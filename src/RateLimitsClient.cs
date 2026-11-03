using System;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Text.Json;

namespace CodexContinue;

/// <summary>
/// Real-time quota channel: a persistent `codex app-server` child speaking newline-delimited
/// JSON-RPC over stdio. `account/rateLimits/read` returns live backend values (usedPercent,
/// resetsAt, windowDurationMins, ordinaryUsageAllowed, rateLimitReachedType) with zero token
/// handling in this process — the engine loads ~/.codex/auth.json itself.
/// Verified live on this machine (0.1-2s per read). The rollout-file parse stays as fallback.
/// </summary>
public sealed class RateLimitsClient : IDisposable
{
    public sealed record Window(double? UsedPercent, DateTime? ResetsAt, long? WindowMinutes);
    public sealed record Reading(Window Primary, Window Secondary, bool? UsageAllowed, string? ReachedType, string PlanType);
    public sealed record AccountInfo(string Email, string PlanType);

    readonly object _gate = new();
    Process? _proc;
    StreamReader? _reader;
    int _nextId = 10;

    public Reading? Read()
    {
        var result = Request("account/rateLimits/read", new { excludeResetCreditDetails = true });
        return result is { } r ? Parse(r) : null;
    }

    /// <summary>Account identity (email + plan) — `account/read`. Null when unavailable.</summary>
    public AccountInfo? ReadAccount()
    {
        var result = Request("account/read", new { });
        if (result is not { } r) return null;
        try
        {
            if (!r.TryGetProperty("account", out var acc)) return null;
            var email = acc.TryGetProperty("email", out var e) ? e.GetString() : null;
            var plan = acc.TryGetProperty("planType", out var p) ? p.GetString() : null;
            if (string.IsNullOrEmpty(email)) return null;
            return new AccountInfo(email, plan ?? "");
        }
        catch { return null; }
    }

    JsonElement? Request(string method, object? args)
    {
        lock (_gate)
        {
            if (!EnsureProcess()) return null;
            int id = _nextId++;
            try
            {
                _proc!.StandardInput.WriteLine(JsonSerializer.Serialize(new Dictionary<string, object?>
                {
                    ["jsonrpc"] = "2.0",
                    ["id"] = id,
                    ["method"] = method,
                    ["params"] = args,
                }));
                _proc.StandardInput.Flush();

                var sw = Stopwatch.StartNew();
                while (sw.Elapsed < TimeSpan.FromSeconds(5))
                {
                    string? line = ReadLine(TimeSpan.FromSeconds(5) - sw.Elapsed);
                    if (line == null) break; // child died
                    JsonDocument doc;
                    try { doc = JsonDocument.Parse(line); } catch { continue; }
                    using (doc)
                    {
                        if (!doc.RootElement.TryGetProperty("id", out var idEl)) continue;
                        if (idEl.GetInt32() != id) continue;
                        if (doc.RootElement.TryGetProperty("error", out _)) return null;
                        // clone out of the disposed doc
                        return doc.RootElement.GetProperty("result").Clone();
                    }
                }
            }
            catch { }
            // timed out or died — drop the process so the next call restarts clean
            Kill();
            return null;
        }
    }

    static Reading Parse(JsonElement result)
    {
        bool? allowed = result.TryGetProperty("ordinaryUsageAllowed", out var a) && (a.ValueKind == JsonValueKind.True || a.ValueKind == JsonValueKind.False)
            ? a.GetBoolean() : null;
        string? reached = null;
        string plan = "";
        Window pri = new(null, null, null), sec = new(null, null, null);

        if (result.TryGetProperty("rateLimits", out var rl))
        {
            if (rl.TryGetProperty("planType", out var p)) plan = p.GetString() ?? "";
            if (rl.TryGetProperty("rateLimitReachedType", out var r) && r.ValueKind == JsonValueKind.String)
                reached = r.GetString();
            pri = ParseWindow(rl, "primary");
            sec = ParseWindow(rl, "secondary");
        }
        return new Reading(pri, sec, allowed, reached, plan);
    }

    static Window ParseWindow(JsonElement rl, string name)
    {
        if (!rl.TryGetProperty(name, out var w)) return new(null, null, null);
        double? used = w.TryGetProperty("usedPercent", out var u) && u.ValueKind == JsonValueKind.Number
            ? u.GetDouble() : null;
        DateTime? resets = null;
        if (w.TryGetProperty("resetsAt", out var rs) && rs.ValueKind == JsonValueKind.Number)
            resets = DateTimeOffset.FromUnixTimeSeconds(rs.GetInt64()).LocalDateTime;
        long? mins = w.TryGetProperty("windowDurationMins", out var m) && m.ValueKind == JsonValueKind.Number
            ? m.GetInt64() : null;
        return new(used, resets, mins);
    }

    string? ReadLine(TimeSpan timeout)
    {
        // Bound the blocking read itself, including initialization. Capture the reader
        // before Kill can clear it so an old read never consumes a new process's output.
        var reader = _reader!;
        if (timeout > TimeSpan.Zero)
        {
            var read = System.Threading.Tasks.Task.Run(reader.ReadLine);
            if (read.Wait(timeout)) return read.Result;
        }
        Kill();
        return null;
    }

    bool EnsureProcess()
    {
        if (_proc is { HasExited: false } && _reader != null) return true;
        Kill();
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = CodexService.FindCodexEngine(),
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            psi.Arguments = "app-server";
            _proc = Process.Start(psi);
            if (_proc == null) return false;
            _reader = _proc.StandardOutput;

            _proc.StandardInput.WriteLine(JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["jsonrpc"] = "2.0",
                ["id"] = 1,
                ["method"] = "initialize",
                ["params"] = new
                {
                    clientInfo = new { name = "CodexContinue", title = "Codex Continue", version = "1.0.0" },
                    capabilities = new { },
                },
            }));
            _proc.StandardInput.Flush();

            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < TimeSpan.FromSeconds(5))
            {
                string? line = ReadLine(TimeSpan.FromSeconds(5) - sw.Elapsed);
                if (line == null) { Kill(); return false; }
                if (line.Contains("\"id\":1")) return true;
            }
        }
        catch { }
        Kill();
        return false;
    }

    void Kill()
    {
        try { if (_proc is { HasExited: false }) _proc.Kill(); } catch { }
        try { _proc?.Dispose(); } catch { }
        _proc = null;
        _reader = null;
    }

    public void Dispose() => Kill();
}
