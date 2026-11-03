using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;

namespace CodexContinue;

public enum QuotaState
{
    Unknown,        // no Codex window / no readable text
    Ready,          // window present, no limit banner
    Limited,        // limit banner present, reset time known
    LimitedNoTime,  // limit banner present, couldn't parse reset time
}

/// <summary>
/// One observation of the Codex quota. UsedPercent = 5h-window percent used (0-100),
/// fresh only as of ObservedAt (rollout files update per API turn, not in real time —
/// BannerVisible is the live signal, percent is the trend).
/// </summary>
public sealed record QuotaSnapshot(
    QuotaState State,
    DateTime? ResetAt,
    string Evidence,
    double? UsedPercent = null,
    double? WeeklyPercent = null,
    bool BannerVisible = false,
    DateTime ObservedAt = default)
{
    public static QuotaSnapshot Unknown(string why) => new(QuotaState.Unknown, null, why);

    /// <summary>
    /// Exhausted = the live banner, or a still-relevant ~100% reading. A stale 100% reading
    /// (rollout files only advance when Codex makes API calls — an idle, recovered Codex leaves
    /// the old number frozen) must NOT count as exhausted once the reset moment has passed,
    /// or the recovery event would never fire.
    /// </summary>
    public bool IsExhausted(DateTime now) =>
        BannerVisible || (UsedPercent >= 99 && ResetAt is { } r && now < r.AddMinutes(2));
}

/// <summary>
/// Talks to the Codex desktop app (Microsoft Store "OpenAI.Codex", process ChatGPT.exe,
/// Electron window Chrome_WidgetWin_1) via UI Automation. Read paths are safe to call any
/// time; write paths (typing) require a foreground grab and never press Enter on their own
/// except through SendContinue().
/// </summary>
public static class CodexService
{
    // Quota banner strings observed live (zh) + English fallbacks.
    // e.g. "你已达到使用上限。升级套餐或充值额度以继续，或在 02:43后重试。"
    // e.g. "添加额度或升级套餐，或等待使用量于 10月27日 2:43 重置"
    // e.g. "Codex 和工作使用额度已用完"
    static readonly Regex ResetFull = new(@"(\d{1,2})月(\d{1,2})日\s*(\d{1,2}):(\d{2})", RegexOptions.Compiled);
    static readonly Regex ResetClock = new(@"(\d{1,2}):(\d{2})\s*(?:后|重置|后重试|时)", RegexOptions.Compiled);
    static readonly Regex LimitMarker = new("(使用上限|额度已用完|使用额度|已达到使用上限|usage limit|rate limit|quota)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static IntPtr FindWindow()
    {
        // 两轮扫描：可见窗优先，托盘/隐藏窗兜底——用户：任务栏关了但后台在跑也必须连上
        IntPtr visible = IntPtr.Zero, hidden = IntPtr.Zero;
        var nameCache = new Dictionary<int, bool>(); // pid -> is ChatGPT/Codex
        var cls = new System.Text.StringBuilder(256);
        Native.EnumWindows((h, l) =>
        {
            // cheap class filter FIRST — resolving the process per window used to cost
            // 25-47ms per sweep and leak a handle each call
            cls.Clear();
            Native.GetClassNameW(h, cls, 256);
            // Electron main window; skip helper/child classes
            if (!cls.ToString().StartsWith("Chrome_WidgetWin", StringComparison.OrdinalIgnoreCase)) return true;
            Native.GetWindowThreadProcessId(h, out uint pid);
            if (!nameCache.TryGetValue((int)pid, out bool match))
            {
                try
                {
                    using var p = Process.GetProcessById((int)pid);
                    match = p.ProcessName.Equals("ChatGPT", StringComparison.OrdinalIgnoreCase)
                        || p.ProcessName.Equals("Codex", StringComparison.OrdinalIgnoreCase);
                }
                catch { match = false; }
                nameCache[(int)pid] = match;
            }
            if (!match) return true;
            if (Native.IsWindowVisible(h)) { if (visible == IntPtr.Zero) visible = h; }
            else if (hidden == IntPtr.Zero) hidden = h;
            return visible == IntPtr.Zero; // keep sweeping until a visible one lands
        }, IntPtr.Zero);
        return visible != IntPtr.Zero ? visible : hidden;
    }

    /// <summary>Codex is up: its Electron window exists (visible or tray-hidden).
    /// 窗口为准——ChatGPT/Codex 的无窗辅助进程会常驻，按进程名判会「假在线」（用户实测）。</summary>
    public static bool IsCodexRunning() => FindWindow() != IntPtr.Zero;

    public static QuotaSnapshot ReadQuota()
    {
        IntPtr hwnd = FindWindow();
        // 窗口是连接的唯一凭证——没有它，本地 rollout 残留/RPC 引擎都会给出
        // 「看似有数」的假额度（用户实测：没开 Codex 也显示在线+额度）。宁可弃权。
        if (hwnd == IntPtr.Zero)
            return QuotaSnapshot.Unknown("未找到 Codex 窗口");

        AutomationElement root;
        try { root = AutomationElement.FromHandle(hwnd); }
        catch (Exception e) { return QuotaSnapshot.Unknown("UIA 连接失败: " + e.Message); }

        // Live limit banner is a StatusBar (class _banner_*) sitting above the composer.
        // Historical turn text like "你已达到使用上限…" stays in the tree forever — do NOT
        // treat plain conversation text as a live signal, or Ready will never be seen again.
        string blob = "";
        try
        {
            var bars = root.FindAll(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.StatusBar));
            var walker = TreeWalker.ControlViewWalker;
            foreach (AutomationElement bar in bars)
            {
                var barCls = bar.Current.ClassName ?? "";
                var stack = new System.Collections.Generic.Stack<AutomationElement>();
                stack.Push(bar);
                while (stack.Count > 0)
                {
                    var el = stack.Pop();
                    var nm = el.Current.Name;
                    if (!string.IsNullOrEmpty(nm) && nm.Length < 220 && LimitMarker.IsMatch(nm))
                        blob += nm + "\n";
                    var child = walker.GetFirstChild(el);
                    while (child != null) { stack.Push(child); child = walker.GetNextSibling(child); }
                }
                // class _banner_ without matching text still counts as a live limit notice
                if (blob.Length == 0 && barCls.Contains("banner"))
                    blob += "(额度横幅在显示)\n";
            }
        }
        catch (Exception e) { return QuotaSnapshot.Unknown("UIA 遍历失败: " + e.Message); }

        bool bannerVisible = blob.Length > 0;

        // Real-time percent + precise reset-at: app-server JSON-RPC first (live backend values),
        // rollout-file parse as fallback (goes stale while Codex is idle).
        var live = RateLimits.Read();
        var rl = live != null
            ? new RolloutLimits(live.Primary.UsedPercent, live.Secondary.UsedPercent, live.Primary.ResetsAt, DateTime.Now)
            : ReadRolloutRateLimits();

        // Backend flag beats UI: ordinaryUsageAllowed=false is exhaustion even before the banner paints.
        if (live?.UsageAllowed == false) bannerVisible = true;

        DateTime? resetAt = null;
        if (bannerVisible)
        {
            var mFull = ResetFull.Match(blob);
            if (mFull.Success)
            {
                int mo = int.Parse(mFull.Groups[1].Value), day = int.Parse(mFull.Groups[2].Value);
                int hh = int.Parse(mFull.Groups[3].Value), mm = int.Parse(mFull.Groups[4].Value);
                try { resetAt = new DateTime(DateTime.Now.Year, mo, day, hh, mm, 0); }
                catch { }
            }
            else
            {
                var mClock = ResetClock.Match(blob);
                if (mClock.Success)
                {
                    int hh = int.Parse(mClock.Groups[1].Value), mm = int.Parse(mClock.Groups[2].Value);
                    var t = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, hh, mm, 0);
                    if (t < DateTime.Now.AddMinutes(-30)) t = t.AddDays(1); // rolled past midnight
                    resetAt = t;
                }
            }
            // rollout resets_at carries seconds — refine the UI minute-level time
            if (rl?.ResetAt is { } precise &&
                (!resetAt.HasValue || Math.Abs((resetAt.Value - precise).TotalMinutes) < 30))
                resetAt = precise;
        }
        else
        {
            resetAt = rl?.ResetAt;
        }

        string evidence = bannerVisible
            ? blob.Trim().Replace("\n", " | ")
            : $"窗口在线，无额度横幅 · 已用 {rl?.UsedPercent:0}%";
        if (evidence.Length > 160) evidence = evidence.Substring(0, 160) + "…";

        var state = !bannerVisible ? QuotaState.Ready
            : resetAt.HasValue ? QuotaState.Limited
            : QuotaState.LimitedNoTime;

        return new QuotaSnapshot(state, resetAt, evidence,
            rl?.UsedPercent, rl?.WeeklyPercent, bannerVisible, rl?.ObservedAt ?? DateTime.Now);
    }

    public sealed record RolloutLimits(double? UsedPercent, double? WeeklyPercent, DateTime? ResetAt, DateTime ObservedAt);

    /// <summary>Shared persistent app-server child for real-time quota reads.</summary>
    static readonly RateLimitsClient RateLimits = new();

    /// <summary>Account identity via app-server `account/read` (email + plan). Null when undetectable.</summary>
    public static RateLimitsClient.AccountInfo? ReadAccount() => RateLimits.ReadAccount();

    /// <summary>A Codex conversation. Running = the conversation is EXECUTING right now
    /// (thinking / calling tools / replying) — NOT merely loaded. 用户口径：只是打开不算运行。</summary>
    public sealed record RunningThread(string Id, string Name, bool Running);

    /// <summary>Window (in seconds) in which a rollout still counts as actively executing —
    /// Codex appends to the rollout continuously while working; a still task stops writing.</summary>
    const int RunningFreshSeconds = 180;

    /// <summary>session uuid → newest rollout mtime. Rollout filenames embed the session uuid;
    /// the file is appended per event, so freshness = the conversation is executing.</summary>
    static Dictionary<string, DateTime> ReadRolloutActivity(string sessionsRoot)
    {
        var act = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (!Directory.Exists(sessionsRoot)) return act;
            var uuidRe = new Regex("[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            foreach (var f in Directory.EnumerateFiles(sessionsRoot, "rollout-*.jsonl", SearchOption.AllDirectories))
            {
                var t = File.GetLastWriteTime(f);
                foreach (Match m in uuidRe.Matches(Path.GetFileName(f)))
                {
                    var id = m.Value;
                    if (!act.TryGetValue(id, out var cur) || t > cur) act[id] = t;
                }
            }
        }
        catch { }
        return act;
    }

    /// <summary>
    /// Every conversation on this machine (session_index.jsonl, id → thread_name), oldest first.
    /// The list itself is Codex's own on-disk history and shows regardless of state.
    /// Running = this conversation's rollout was written within RunningFreshSeconds ——正在思考/
    /// 调用工具/回复中才会持续写盘。writer-lock 不作数：它是「会话被加载」的残留（0 字节、
    /// 陈旧 lock 一堆），用户明文「只是打开不算运行」。Unnamed threads fall back to a short id
    /// label. session_index keeps one row PER SESSION of a thread (one name can own hundreds of
    /// ids) — collapse to one entry per conversation name; the freshest session represents it.
    /// </summary>
    public static List<RunningThread> AllThreads()
    {
        var result = new List<RunningThread>();
        try
        {
            // offline: nothing exists as far as the app is concerned — an empty picker
            // beats a history list pretending to be live
            bool codexAlive = IsCodexRunning();
            if (!codexAlive) return result;

            string home = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");
            string lockDir = Path.Combine(home, "thread-writer-locks");
            var names = ReadSessionIndexNames(home);
            var activity = ReadRolloutActivity(Path.Combine(home, "sessions"));
            var now = DateTime.Now;

            var byName = new Dictionary<string, RunningThread>(StringComparer.OrdinalIgnoreCase);
            var order = new List<string>();
            foreach (var kv in names)
            {
                bool running = activity.TryGetValue(kv.Key, out var last)
                    && (now - last).TotalSeconds <= RunningFreshSeconds;
                if (!byName.TryGetValue(kv.Value, out var cur))
                {
                    order.Add(kv.Value);
                    byName[kv.Value] = new RunningThread(kv.Key, kv.Value, running);
                }
                else
                {
                    // a running id is the active one; otherwise keep the first-seen id
                    string id = running ? kv.Key : cur.Id;
                    byName[kv.Value] = new RunningThread(id, kv.Value, cur.Running || running);
                }
            }
            foreach (var n in order) result.Add(byName[n]);

            // locked conversations missing from the index still get listed — but "running"
            // is judged by rollout freshness like everyone else (lock ≠ executing)
            if (codexAlive && Directory.Exists(lockDir))
            {
                foreach (var f in Directory.EnumerateFiles(lockDir, "*.lock"))
                {
                    string id = Path.GetFileNameWithoutExtension(f);
                    if (id == ".coordination") continue;
                    if (result.Exists(t => t.Id == id)) continue;
                    bool running = activity.TryGetValue(id, out var last2)
                        && (now - last2).TotalSeconds <= RunningFreshSeconds;
                    result.Add(new RunningThread(id, "对话 " + id.Substring(0, Math.Min(8, id.Length)), running));
                }
            }
        }
        catch { }
        return result;
    }

    /// <summary>Conversations currently loaded in the Codex desktop app (writer lock held).</summary>
    public static List<RunningThread> RunningThreads() =>
        AllThreads().FindAll(t => t.Running);

    static Dictionary<string, string> ReadSessionIndexNames(string codexHome)
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            string idx = Path.Combine(codexHome, "session_index.jsonl");
            if (!File.Exists(idx)) return names;
            foreach (var line in File.ReadLines(idx))
            {
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(line);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("id", out var idEl) && root.TryGetProperty("thread_name", out var nEl))
                    {
                        var id = idEl.GetString();
                        var nm = nEl.GetString();
                        if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(nm)) names[id] = nm;
                    }
                }
                catch { }
            }
        }
        catch { }
        return names;
    }

    /// <summary>
    /// Newest rollout-*.jsonl under %USERPROFILE%\.codex\sessions\… carries
    /// payload.rate_limits = {"primary":{"used_percent":…,"window_minutes":300,"resets_at":<unix>},
    /// "secondary":{…window_minutes:10080…}} inside token_count events. Windows are classified by
    /// window_minutes (300 = the 5h bucket), not slot order. Read the file tail only.
    /// </summary>
    public static RolloutLimits? ReadRolloutRateLimits()
    {
        try
        {
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex", "sessions");
            if (!Directory.Exists(root)) return null;
            string? newest = null;
            DateTime newestT = DateTime.MinValue;
            foreach (var f in Directory.EnumerateFiles(root, "rollout-*.jsonl", SearchOption.AllDirectories))
            {
                var t = File.GetLastWriteTime(f);
                if (t > newestT) { newestT = t; newest = f; }
            }
            if (newest == null) return null;

            using var fs = new FileStream(newest, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            long start = Math.Max(0, fs.Length - 256 * 1024);
            fs.Seek(start, SeekOrigin.Begin);
            using var sr = new StreamReader(fs);
            string tail = sr.ReadToEnd();

            // last rate_limits occurrence wins; capture each window block
            var rePrimary = new Regex("\"primary\"\\s*:\\s*\\{\\s*\"used_percent\"\\s*:\\s*([\\d.]+)[^}]*?\"window_minutes\"\\s*:\\s*(\\d+)[^}]*?\"resets_at\"\\s*:\\s*(\\d+)", RegexOptions.Compiled);
            var reSecondary = new Regex("\"secondary\"\\s*:\\s*\\{\\s*\"used_percent\"\\s*:\\s*([\\d.]+)[^}]*?\"window_minutes\"\\s*:\\s*(\\d+)[^}]*?\"resets_at\"\\s*:\\s*(\\d+)", RegexOptions.Compiled);
            var reAny = new Regex("\"used_percent\"\\s*:\\s*([\\d.]+)[^}]*?\"window_minutes\"\\s*:\\s*(\\d+)[^}]*?\"resets_at\"\\s*:\\s*(\\d+)", RegexOptions.Compiled);

            var lines = tail.Split('\n');
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                var mp = rePrimary.Match(lines[i]);
                if (!mp.Success) continue;

                double used = double.Parse(mp.Groups[1].Value, CultureInfo.InvariantCulture);
                var resetAt = DateTimeOffset.FromUnixTimeSeconds(long.Parse(mp.Groups[3].Value)).LocalDateTime;

                double? weekly = null;
                var ms = reSecondary.Match(lines[i]);
                if (ms.Success) weekly = double.Parse(ms.Groups[1].Value, CultureInfo.InvariantCulture);
                else
                {
                    // fall back: any non-primary window with a weekly-sized window_minutes
                    foreach (Match m in reAny.Matches(lines[i]))
                    {
                        if (long.Parse(m.Groups[2].Value) >= 10000)
                        { weekly = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture); break; }
                    }
                }

                return new RolloutLimits(used, weekly, resetAt, newestT);
            }
        }
        catch { }
        return null;
    }

    /// <summary>Assert-based checks for the pure helpers; runs once at startup (debug builds).</summary>
    [System.Diagnostics.Conditional("DEBUG")]
    public static void DebugSelfCheck()
    {
        System.Diagnostics.Debug.Assert(ComposeMessage("/continue", null) == "/continue");
        System.Diagnostics.Debug.Assert(ComposeMessage(" hi ", new string[0]) == "hi");
        System.Diagnostics.Debug.Assert(ComposeMessage("", new[] { "C:\\a.png" }) == "附件:\nC:\\a.png");
        System.Diagnostics.Debug.Assert(ComposeMessage("go", new[] { "C:\\a.png", "D:\\b.txt" }) == "go\n\n附件:\nC:\\a.png\nD:\\b.txt");
    }

    /// <summary>Convenience wrapper kept for callers that only need the reset moment.</summary>
    public static DateTime? ReadRolloutResetAt() => ReadRolloutRateLimits()?.ResetAt;

    /// <summary>
    /// Compose the queued message: prompt text plus attachment paths on their own line.
    /// Attachments travel as paths in the message body (user decision 2026-10-27) — Codex
    /// reads local files itself. Pure function; self-checked by DebugSelfCheck.
    /// </summary>
    public static string ComposeMessage(string prompt, IReadOnlyList<string>? attachmentPaths)
    {
        string body = prompt.Trim();
        if (attachmentPaths == null || attachmentPaths.Count == 0) return body;
        string list = string.Join("\n", attachmentPaths);
        return body.Length == 0 ? "附件:\n" + list : body + "\n\n附件:\n" + list;
    }

    /// <summary>
    /// True when the newest assistant-sentence in the conversation is a rate-limit error —
    /// i.e. Codex stopped ON a limit. Combined with an available quota this means "recovery
    /// already happened while we weren't watching" and the guard should continue right away.
    /// Candidates exclude composer text (Edit subtree), the live banner (StatusBar subtree),
    /// diff/file rows ("app/src/…" and "+498 -22" stats) and the composer toolbar band —
    /// only real sentences compete, so the limit message wins even when a diff card sits
    /// below it inside the same turn.
    /// </summary>
    public static bool LastTurnIsLimitError()
    {
        IntPtr hwnd = FindWindow();
        if (hwnd == IntPtr.Zero) return false;
        AutomationElement root;
        try { root = AutomationElement.FromHandle(hwnd); } catch { return false; }

        try
        {
            var composer = FindComposer(root);
            double composerTop = composer != null ? composer.Current.BoundingRectangle.Y : double.MaxValue;

            var texts = root.FindAll(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text));
            double bestY = double.MinValue;
            bool bestIsLimit = false;
            var walker = TreeWalker.ControlViewWalker;

            foreach (AutomationElement t in texts)
            {
                var nm = t.Current.Name ?? "";
                if (nm.Length < 15 || nm.Length > 400) continue;
                if (nm.Contains('+') || nm.Contains('/') || nm.Contains('\\')) continue; // diff rows / paths

                bool skip = false;
                var p = walker.GetParent(t);
                for (int up = 0; up < 8 && p != null; up++)
                {
                    var ct = p.Current.ControlType;
                    if (ct == ControlType.Edit || ct == ControlType.StatusBar) { skip = true; break; }
                    p = walker.GetParent(p);
                }
                if (skip) continue;

                var r = t.Current.BoundingRectangle;
                if (r.IsEmpty || r.Height < 8) continue;
                if (r.Y >= composerTop - 80) continue; // composer toolbar band
                if (r.Y < bestY) continue;

                bestY = r.Y;
                bestIsLimit = LimitMarker.IsMatch(nm);
            }
            return bestIsLimit;
        }
        catch { return false; }
    }

    static AutomationElement? FindComposer(AutomationElement root)
    {
        var edits = root.FindAll(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit));
        foreach (AutomationElement e in edits)
        {
            // className is "ProseMirror" or "ProseMirror ProseMirror-focused" — must Contains-match
            var cls = e.Current.ClassName ?? "";
            if (cls.Contains("ProseMirror")) return e;
        }
        return null;
    }

    /// <summary>Grab foreground for the Codex window (restore if minimized). Returns success.</summary>
    public static bool ForceForeground(IntPtr hwnd)
    {
        if (Native.IsIconic(hwnd)) Native.ShowWindow(hwnd, 9); // SW_RESTORE
        uint dummy;
        uint fgTid = Native.GetWindowThreadProcessId(Native.GetForegroundWindow(), out dummy);
        uint myTid = Native.GetCurrentThreadId();
        Native.AttachThreadInput(myTid, fgTid, true);
        Native.SetForegroundWindow(hwnd);
        Native.BringWindowToTop(hwnd);
        Native.AttachThreadInput(myTid, fgTid, false);
        Thread.Sleep(200);
        return Native.GetForegroundWindow() == hwnd;
    }

    public enum SendOutcome { Sent, BlockedByLimit, Failed, Unknown }

    public sealed record SendResult(SendOutcome Outcome, string Log);

    static string B(string zh, string en) => Loc.IsZh() ? zh : en;

    /// <summary>
    /// Type the message into the composer and submit. Uses SendInput unicode (ValuePattern.SetValue
    /// does NOT work on ProseMirror contenteditable — verified live). Submit prefers the "发送"
    /// Invoke button, falls back to Enter + a mouse click. Verifies the composer drained after submit.
    /// If the send button is disabled (Codex does this while the quota banner is up — verified live:
    /// Enter is also swallowed), returns BlockedByLimit so the caller can wait instead of burning retries.
    /// </summary>
    public static Task<SendResult> SendContinue(string message, bool pressEnter = true)
    {
        return Task.Run(() =>
        {
            IntPtr hwnd = FindWindow();
            if (hwnd == IntPtr.Zero) return new SendResult(SendOutcome.Failed, B("未找到 Codex 窗口", "Codex window not found"));

            AutomationElement root;
            try { root = AutomationElement.FromHandle(hwnd); } catch (Exception e) { return new SendResult(SendOutcome.Failed, B("UIA 失败: ", "UIA failed: ") + e.Message); }

            var composer = FindComposer(root);
            if (composer == null) return new SendResult(SendOutcome.Failed, B("未找到输入框 (ProseMirror)", "Composer (ProseMirror) not found"));

            if (!ForceForeground(hwnd)) return new SendResult(SendOutcome.Failed, B("无法获取前台窗口（可能被全屏应用占用）", "Cannot take foreground (a fullscreen app may hold it)"));

            try { composer.SetFocus(); } catch { }
            Thread.Sleep(150);

            // safety: only type into the window we just focused
            if (Native.GetForegroundWindow() != hwnd) return new SendResult(SendOutcome.Failed, B("焦点被抢走，已取消输入", "Focus stolen, input cancelled"));

            // if a draft is sitting in the composer, clear it first so we don't append to it
            string placeholder = (composer.Current.Name ?? "").Trim();
            string before = ReadComposer(composer);
            bool empty = string.IsNullOrWhiteSpace(before) || before.Trim() == placeholder;
            string clearedNote = "";
            if (!empty)
            {
                Native.Key(Native.VK_CONTROL, true);
                Native.Key(Native.VK_KEY_A);
                Native.Key(Native.VK_CONTROL, false);
                Thread.Sleep(80);
                Native.Key(Native.VK_DELETE);
                Thread.Sleep(150);
                clearedNote = B("（已清空输入框原有草稿）", " (cleared existing draft)");
            }

            Native.TypeText(message);
            Thread.Sleep(300);

            if (!ReadComposer(composer).Contains(message))
                return new SendResult(SendOutcome.Failed, B("输入校验失败", "Input verification failed") + clearedNote + B("，未提交", ", not submitted"));

            if (!pressEnter)
                return new SendResult(SendOutcome.Sent, B("已输入（未发送）", "Typed (not sent)") + clearedNote + ": " + message);

            var sendBtn = FindSendButton(root, composer);

            // Note: Chromium may report IsEnabled=false even after quota recovers until the UI
            // refreshes — don't hard-fail on it. Attempt submit; only BlockedByLimit if the
            // composer refuses to drain.
            bool btnDisabled = sendBtn != null && !sendBtn.Current.IsEnabled;

            string method = "Enter";
            if (sendBtn != null)
            {
                try
                {
                    ((InvokePattern)sendBtn.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
                    method = B("按钮", "button");
                    Thread.Sleep(500);
                }
                catch { /* fall through */ }
            }

            if (ReadComposer(composer).Contains(message))
            {
                Native.Key(Native.VK_RETURN); // VK
                Thread.Sleep(500);
            }
            if (ReadComposer(composer).Contains(message))
            {
                var down = new Native.INPUT { type = Native.INPUT_KEYBOARD, ki = new Native.KEYBDINPUT { wScan = 0x1C, dwFlags = Native.KEYEVENTF_SCANCODE } };
                var up = down; up.ki.dwFlags = Native.KEYEVENTF_SCANCODE | Native.KEYEVENTF_KEYUP;
                Native.SendInput(2, new[] { down, up }, Marshal.SizeOf(typeof(Native.INPUT))); // Enter via scancode
                Thread.Sleep(500);
                method = B("Enter(扫描码)", "Enter(scancode)");
            }
            if (ReadComposer(composer).Contains(message) && sendBtn != null)
            {
                // last resort: physical click on the button center
                var r = sendBtn.Current.BoundingRectangle;
                int x = (int)(r.X + r.Width / 2), y = (int)(r.Y + r.Height / 2);
                Native.SetCursorPos(x, y);
                Thread.Sleep(80);
                Native.mouse_event(Native.MOUSEEVENTF_LEFTDOWN, 0, 0, 0, IntPtr.Zero);
                Native.mouse_event(Native.MOUSEEVENTF_LEFTUP, 0, 0, 0, IntPtr.Zero);
                method = B("点击按钮", "click button");
                Thread.Sleep(500);
            }

            if (ReadComposer(composer).Contains(message))
            {
                // nothing submitted → limit state hasn't flipped yet; don't leave junk
                ClearComposer(composer);
                return new SendResult(SendOutcome.BlockedByLimit,
                    B("提交未生效（发送按钮", "Submit had no effect (send button ") + (btnDisabled ? B("禁用", "disabled") : B("启用但无响应", "enabled but unresponsive")) + B("）", ")") + clearedNote + B("，多半是额度还没真正恢复", " - quota likely has not recovered"));
            }
            return new SendResult(SendOutcome.Sent, B("已发送（", "Sent (") + method + B("）", ")") + clearedNote + ": " + message);
        });
    }

    static void ClearComposer(AutomationElement composer)
    {
        try
        {
            Native.Key(Native.VK_CONTROL, true);
            Native.Key(Native.VK_KEY_A);
            Native.Key(Native.VK_CONTROL, false);
            Thread.Sleep(80);
            Native.Key(Native.VK_DELETE);
            Thread.Sleep(120);
        }
        catch { }
    }

    // ---------- queue fallback (no UI / no focus needed) ----------
    // `codex queue --thread <uuid> --message <msg>` rides the local CODEX_HOME queue DB and
    // delivers as a real user turn while the conversation is open in the desktop app
    // (verified: thread-writer-locks/<uuid>.lock held). Works even when keyboard injection can't.

    public static Task<SendResult> QueueSend(string message, string? threadId = null)
    {
        return Task.Run(async () =>
        {
            threadId ??= FindDesktopThreadId();
            if (threadId == null)
                return new SendResult(SendOutcome.Failed, B("队列注入：无法确定会话 UUID", "Queue inject: cannot determine session UUID"));

            string lockPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".codex", "thread-writer-locks", threadId + ".lock");
            if (!File.Exists(lockPath))
                return new SendResult(SendOutcome.Failed, B("队列注入：会话未在 Codex 中加载，已取消（防止消息滞留）", "Queue inject: session not loaded in Codex, cancelled to avoid stranding"));

            string exe = FindCodexEngine();
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = "queue --thread " + threadId + " --message " + QuoteArg(message),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };
                using var p = Process.Start(psi);
                if (p == null) return new SendResult(SendOutcome.Failed, B("队列注入：无法启动 ", "Queue inject: cannot start ") + exe);
                var result = await WaitForQueueExitAsync(p, TimeSpan.FromSeconds(15));
                if (result.Outcome != SendOutcome.Sent) return result;
                return new SendResult(SendOutcome.Sent, B("已通过队列注入（codex queue → ", "Queued (codex queue -> ") + threadId.Substring(0, 8) + B("…）", "...)") + ": " + message);
            }
            catch (Exception e)
            {
                return new SendResult(SendOutcome.Failed, B("队列注入异常: ", "Queue inject exception: ") + e.Message);
            }
        });
    }

    static string QuoteArg(string s) => "\"" + s.Replace("\"", "\\\"") + "\"";

    static async Task<SendResult> WaitForQueueExitAsync(Process process, TimeSpan timeout)
    {
        using var deadline = new CancellationTokenSource(timeout);
        var stdout = process.StandardOutput.ReadToEndAsync(deadline.Token);
        var stderr = process.StandardError.ReadToEndAsync(deadline.Token);
        try
        {
            await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
            string output = ((await stdout.ConfigureAwait(false)) + " " + (await stderr.ConfigureAwait(false))).Trim();
            if (process.ExitCode == 0 && !output.Contains("No active session"))
                return new SendResult(SendOutcome.Sent, B("指令已提交至发送队列", "Command submitted to the send queue"));
            return new SendResult(SendOutcome.Failed, B("队列注入失败: ", "Queue inject failed: ") + (output.Length > 120 ? output[..120] : output));
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            try { await Task.WhenAll(stdout, stderr).ConfigureAwait(false); } catch { }
            return new SendResult(SendOutcome.Unknown, B("队列提交超时，发送结果未能确认", "Queue submission timed out; delivery could not be confirmed"));
        }
        catch (Exception e)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            return new SendResult(SendOutcome.Unknown, B("队列发送结果未能确认: ", "Queue delivery could not be confirmed: ") + e.Message);
        }
    }

    /// <summary>Root thread id of the currently active desktop conversation (from the newest rollout head).</summary>
    public static string? FindDesktopThreadId()
    {
        try
        {
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex", "sessions");
            if (!Directory.Exists(root)) return null;
            string? newest = null;
            DateTime newestT = DateTime.MinValue;
            foreach (var f in Directory.EnumerateFiles(root, "rollout-*.jsonl", SearchOption.AllDirectories))
            {
                var t = File.GetLastWriteTime(f);
                if (t > newestT) { newestT = t; newest = f; }
            }
            if (newest == null) return null;

            string head;
            using (var fs = new FileStream(newest, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var sr = new StreamReader(fs))
            {
                char[] buf = new char[4096];
                int n = sr.Read(buf, 0, buf.Length);
                head = new string(buf, 0, n);
            }
            var m = Regex.Match(head, "\"session_id\"\\s*:\\s*\"([0-9a-fA-F-]{36})\"");
            if (m.Success) return m.Groups[1].Value;

            // fallback: uuid embedded in the rollout filename
            var mf = Regex.Match(Path.GetFileNameWithoutExtension(newest),
                "([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})");
            return mf.Success ? mf.Groups[1].Value : null;
        }
        catch { return null; }
    }

    /// <summary>Prefer the desktop engine binary (shares CODEX_HOME with npm CLI); else PATH `codex`.</summary>
    public static string FindCodexEngine()
    {
        try
        {
            string binRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "OpenAI", "Codex", "bin");
            if (Directory.Exists(binRoot))
            {
                string? best = null;
                DateTime bestT = DateTime.MinValue;
                foreach (var exe in Directory.EnumerateFiles(binRoot, "codex.exe", SearchOption.AllDirectories))
                {
                    var t = File.GetLastWriteTime(exe);
                    if (t > bestT) { bestT = t; best = exe; }
                }
                if (best != null) return best;
            }
        }
        catch { }
        return "codex";
    }

    static string ReadComposer(AutomationElement composer)
    {
        try
        {
            var tp = composer.GetCurrentPattern(TextPattern.Pattern) as TextPattern;
            if (tp != null) return tp.DocumentRange.GetText(-1) ?? "";
        }
        catch { }
        try
        {
            var vp = composer.GetCurrentPattern(ValuePattern.Pattern) as ValuePattern;
            if (vp != null) return vp.Current.Value ?? "";
        }
        catch { }
        return "";
    }

    static AutomationElement? FindSendButton(AutomationElement root, AutomationElement composer)
    {
        double cx = composer.Current.BoundingRectangle.X + composer.Current.BoundingRectangle.Width;
        double cy = composer.Current.BoundingRectangle.Y + composer.Current.BoundingRectangle.Height / 2;
        AutomationElement? best = null;
        double bestDist = double.MaxValue;
        try
        {
            var btns = root.FindAll(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button));
            foreach (AutomationElement b in btns)
            {
                var nm = b.Current.Name ?? "";
                if (nm != "发送" && !nm.Equals("Send", StringComparison.OrdinalIgnoreCase)) continue;
                try { if (b.GetCurrentPattern(InvokePattern.Pattern) == null) continue; } catch { continue; }
                var r = b.Current.BoundingRectangle;
                double dx = r.X - cx, dy = r.Y + r.Height / 2 - cy;
                double dist = dx * dx + dy * dy;
                if (dist < bestDist) { bestDist = dist; best = b; }
            }
        }
        catch { }
        return best;
    }
}
