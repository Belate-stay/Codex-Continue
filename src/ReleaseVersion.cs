using System.Text.RegularExpressions;

namespace CodexContinue;

public sealed class ReleaseVersion : IComparable<ReleaseVersion>
{
    readonly Version _core;
    readonly string[] _pre;
    ReleaseVersion(Version core, string[] pre) { _core = core; _pre = pre; }
    public static ReleaseVersion? Parse(string value)
    {
        value = Regex.Replace(value.Trim(), @"^Version\s+", "", RegexOptions.IgnoreCase);
        value = Regex.Replace(value.Trim(), @"^Beta\s+(\d+\.\d+\.\d+)$", "$1-beta", RegexOptions.IgnoreCase).TrimStart('v', 'V');
        value = Regex.Replace(value, @"^(\d+)\.(\d+)(?=$|[-+])", "$1.$2.0");
        var match = Regex.Match(value, @"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$");
        if (!match.Success || !Version.TryParse(string.Join('.', match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value), out var core)) return null;
        var pre = match.Groups[4].Success ? match.Groups[4].Value.Split('.') : Array.Empty<string>();
        if (pre.Any(p => Numeric(p) && p.Length > 1 && p[0] == '0')) return null;
        if (pre.Length > 0 && pre[0].Equals("beta", StringComparison.OrdinalIgnoreCase)) pre[0] = "beta";
        return new ReleaseVersion(core, pre);
    }
    static bool Numeric(string value) => value.All(c => c is >= '0' and <= '9');
    public string Label => _pre.FirstOrDefault() == "beta"
        ? "Beta " + _core.ToString(3) + (_pre.Length > 1 ? "." + string.Join('.', _pre.Skip(1)) : "")
        : _core.ToString(3) + (_pre.Length > 0 ? "-" + string.Join('.', _pre) : "");
    public int CompareTo(ReleaseVersion? other)
    {
        if (other == null) return 1;
        int result = _core.CompareTo(other._core);
        if (result != 0) return result;
        if (_pre.Length == 0 || other._pre.Length == 0) return (_pre.Length == 0 ? 1 : 0) - (other._pre.Length == 0 ? 1 : 0);
        for (int i = 0; i < Math.Min(_pre.Length, other._pre.Length); i++)
        {
            string a = _pre[i], b = other._pre[i];
            bool an = Numeric(a), bn = Numeric(b);
            result = an && bn ? a.Length.CompareTo(b.Length) : an != bn ? (an ? -1 : 1) : 0;
            if (result == 0) result = string.Compare(a, b, StringComparison.Ordinal);
            if (result != 0) return result;
        }
        return _pre.Length.CompareTo(other._pre.Length);
    }
}

public sealed record UpdateAsset(string Name, Uri DownloadUrl, long Size, string? Sha256);
public sealed record UpdateRelease(string TagName, string VersionLabel, DateTimeOffset PublishedAt, string Title, string Notes, UpdateAsset? Asset, bool IsPreview = false)
{
    public static UpdateRelease Preview => new("v0.9.2-beta", "Beta 0.9.2", DateTimeOffset.Now, "Beta 0.9.2", "", null, true);
}
public readonly record struct UpdateDownloadProgress(long Received, long? Total);
