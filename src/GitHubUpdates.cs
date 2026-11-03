using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CodexContinue;

public sealed class GitHubUpdates : IDisposable
{
    const string Repository = "Belate-stay/Codex-Continue";
    readonly HttpClient _http;
    readonly bool _ownsHttp;
    public GitHubUpdates(HttpClient? http = null)
    {
        _ownsHttp = http == null;
        _http = http ?? new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
    }
    static HttpRequestMessage Request(Uri uri, bool api)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.UserAgent.ParseAdd("Codex-Continue/1.0");
        request.Headers.Accept.ParseAdd(api ? "application/vnd.github+json" : "application/octet-stream");
        if (api) request.Headers.Add("X-GitHub-Api-Version", "2026-03-10");
        return request;
    }
    public async Task<UpdateRelease?> FindUpdateAsync(ReleaseVersion current, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        using var request = Request(new Uri($"https://api.github.com/repos/{Repository}/releases?per_page=100"), true);
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(deadline.Token).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: deadline.Token).ConfigureAwait(false);
        if (document.RootElement.ValueKind != JsonValueKind.Array) throw new InvalidDataException("Invalid release response.");
        UpdateRelease? newest = null;
        ReleaseVersion best = current;
        foreach (var item in document.RootElement.EnumerateArray())
        {
            if (item.TryGetProperty("draft", out var draft) && draft.ValueKind == JsonValueKind.True) continue;
            string tag = Text(item, "tag_name");
            var version = ReleaseVersion.Parse(tag);
            if (version == null || version.CompareTo(best) <= 0 || !DateTimeOffset.TryParse(Text(item, "published_at"), out var published)) continue;
            var asset = item.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array
                ? assets.EnumerateArray().Select(ParseAsset).Where(a => a != null).OrderByDescending(a => Score(a!.Name)).FirstOrDefault() : null;
            string title = Text(item, "name");
            newest = new UpdateRelease(tag, version.Label, published, string.IsNullOrWhiteSpace(title) ? version.Label : title, Text(item, "body"), asset);
            best = version;
        }
        return newest;
    }
    static string Text(JsonElement item, string name) => item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";
    static UpdateAsset? ParseAsset(JsonElement item)
    {
        string name = Text(item, "name"), lower = name.ToLowerInvariant();
        string product = lower.Replace("-", "").Replace("_", "").Replace(" ", "");
        if (name != Path.GetFileName(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || !product.StartsWith("codexcontinue")) return null;
        if (new[] { "linux", "macos", "darwin", "osx", "arm64", "aarch64", "symbols", "source" }.Any(lower.Contains)) return null;
        if (Path.GetExtension(lower) is not (".exe" or ".msi" or ".zip")) return null;
        if (!Uri.TryCreate(Text(item, "browser_download_url"), UriKind.Absolute, out var url) || url.Scheme != "https" ||
            !url.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) || !url.AbsolutePath.StartsWith($"/{Repository}/releases/download/", StringComparison.OrdinalIgnoreCase)) return null;
        long size = item.TryGetProperty("size", out var length) && length.TryGetInt64(out long bytes) ? bytes : 0;
        string digest = Text(item, "digest");
        string? hash = digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) ? digest[7..] : null;
        if (hash != null && !Regex.IsMatch(hash, "^[0-9a-fA-F]{64}$")) return null;
        return new UpdateAsset(name, url, size, hash);
    }
    static int Score(string name)
    {
        name = name.ToLowerInvariant();
        return (Path.GetExtension(name) == ".exe" ? 100 : Path.GetExtension(name) == ".msi" ? 90 : 80)
            + (name.Contains("setup") || name.Contains("installer") ? 20 : 0) + (name.Contains("x64") || name.Contains("amd64") ? 5 : 0);
    }
    public async Task<string> DownloadAsync(UpdateRelease release, string directory, IProgress<UpdateDownloadProgress>? progress, CancellationToken cancellationToken)
    {
        var asset = release.Asset ?? throw new InvalidOperationException("No compatible package.");
        string versionDirectory = Path.Combine(Path.GetFullPath(directory), release.VersionLabel.Replace(' ', '-'));
        Directory.CreateDirectory(versionDirectory);
        string path = Path.Combine(versionDirectory, asset.Name), partial = path + ".part";
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(10));
        var token = deadline.Token;
        if (File.Exists(path) && await VerifyAsync(path, asset, token).ConfigureAwait(false)) return path;
        try
        {
            using var request = Request(asset.DownloadUrl, false);
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            long? total = asset.Size > 0 ? asset.Size : response.Content.Headers.ContentLength;
            await using (var source = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false))
            await using (var output = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
            {
                var buffer = new byte[81920];
                long received = 0;
                int count;
                while ((count = await source.ReadAsync(buffer, token).ConfigureAwait(false)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, count), token).ConfigureAwait(false);
                    received += count;
                    progress?.Report(new UpdateDownloadProgress(received, total));
                }
                if (total.HasValue && received != total.Value) throw new InvalidDataException("Incomplete download.");
            }
            if (!await VerifyAsync(partial, asset, token).ConfigureAwait(false)) throw new InvalidDataException("Package verification failed.");
            token.ThrowIfCancellationRequested();
            File.Move(partial, path, true);
            return path;
        }
        finally { if (File.Exists(partial)) File.Delete(partial); }
    }
    static async Task<bool> VerifyAsync(string path, UpdateAsset asset, CancellationToken token)
    {
        if (asset.Size > 0 && new FileInfo(path).Length != asset.Size) return false;
        if (asset.Sha256 == null) return true;
        await using var stream = File.OpenRead(path);
        string actual = Convert.ToHexString(await SHA256.HashDataAsync(stream, token).ConfigureAwait(false));
        return actual.Equals(asset.Sha256, StringComparison.OrdinalIgnoreCase);
    }
    public void Dispose() { if (_ownsHttp) _http.Dispose(); }
}
