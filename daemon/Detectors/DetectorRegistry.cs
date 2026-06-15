using System.Text.RegularExpressions;
using Clipwell.Protocol;
using Clipwell.Protocol.Plugins;

namespace Clipwell.Daemon.Detectors;

/// <summary>
/// Holds the ordered set of <see cref="IClipDetector"/>s and classifies an item
/// into a <c>Kind</c>. Built-in detectors ship here; plugins can add more (Phase 2).
/// </summary>
public sealed class DetectorRegistry
{
    private readonly IReadOnlyList<IClipDetector> _detectors;

    public DetectorRegistry(IEnumerable<IClipDetector>? extra = null)
    {
        var all = new List<IClipDetector>
        {
            new ImageDetector(),
            new GitHubPrDetector(),
            new UrlDetector(),
            new EmailDetector(),
            new JiraIssueDetector(),
            new ColorDetector(),
            new PathDetector(),
            new CodeDetector(),
        };
        if (extra is not null) all.AddRange(extra);
        _detectors = all.OrderBy(d => d.Priority).ToList();
    }

    /// <summary>Returns the first matching kind, or "text" as the fallback.</summary>
    public string Classify(ClipItem item)
    {
        foreach (var d in _detectors)
        {
            var kind = d.Detect(item);
            if (kind is not null) return kind;
        }
        return "text";
    }
}

internal sealed class ImageDetector : IClipDetector
{
    public string Id => "builtin.image";
    public int Priority => 0;
    public string? Detect(ClipItem item) => item.HasImage ? "image" : null;
}

internal sealed partial class GitHubPrDetector : IClipDetector
{
    public string Id => "builtin.github-pr";
    public int Priority => 5; // before the generic URL detector
    public string? Detect(ClipItem item) =>
        item.TextContent is { } t && GitHubPrRegex().IsMatch(t.Trim()) ? "github-pr" : null;

    [GeneratedRegex(@"^https?://github\.com/[^/\s]+/[^/\s]+/pull/\d+", RegexOptions.IgnoreCase)]
    private static partial Regex GitHubPrRegex();
}

internal sealed partial class JiraIssueDetector : IClipDetector
{
    public string Id => "builtin.jira-issue";
    public int Priority => 25;
    public string? Detect(ClipItem item) =>
        item.TextContent is { } t && JiraRegex().IsMatch(t.Trim()) ? "jira-issue" : null;

    [GeneratedRegex(@"^[A-Z][A-Z0-9]+-\d+$")]
    private static partial Regex JiraRegex();
}

internal sealed partial class UrlDetector : IClipDetector
{
    public string Id => "builtin.url";
    public int Priority => 10;
    public string? Detect(ClipItem item) =>
        item.TextContent is { } t && UrlRegex().IsMatch(t.Trim()) ? "url" : null;

    [GeneratedRegex(@"^https?://\S+$", RegexOptions.IgnoreCase)]
    private static partial Regex UrlRegex();
}

internal sealed partial class EmailDetector : IClipDetector
{
    public string Id => "builtin.email";
    public int Priority => 20;
    public string? Detect(ClipItem item) =>
        item.TextContent is { } t && EmailRegex().IsMatch(t.Trim()) ? "email" : null;

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailRegex();
}

internal sealed partial class ColorDetector : IClipDetector
{
    public string Id => "builtin.color";
    public int Priority => 30;
    public string? Detect(ClipItem item) =>
        item.TextContent is { } t && ColorRegex().IsMatch(t.Trim()) ? "color" : null;

    [GeneratedRegex(@"^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$")]
    private static partial Regex ColorRegex();
}

internal sealed partial class PathDetector : IClipDetector
{
    public string Id => "builtin.path";
    public int Priority => 40;
    public string? Detect(ClipItem item)
    {
        if (item.TextContent is not { } t) return null;
        t = t.Trim();
        if (t.Contains('\n')) return null;
        // Windows drive path, UNC, or POSIX absolute path.
        return WinPath().IsMatch(t) || t.StartsWith(@"\\") || PosixPath().IsMatch(t)
            ? "path"
            : null;
    }

    [GeneratedRegex(@"^[a-zA-Z]:[\\/].+")]
    private static partial Regex WinPath();

    [GeneratedRegex(@"^/(?:[^/\0]+/)*[^/\0]+$")]
    private static partial Regex PosixPath();
}

internal sealed class CodeDetector : IClipDetector
{
    public string Id => "builtin.code";
    public int Priority => 50;

    public string? Detect(ClipItem item)
    {
        if (item.TextContent is not { } t || t.Length < 3) return null;
        // Heuristic: code tends to have braces/semicolons, or multiple lines where
        // some are indented.
        var hasCodeChars = t.Contains(';') || t.Contains('{') || t.Contains("=>") ||
                           t.Contains("function ") || t.Contains("def ") || t.Contains("</");
        var lines = t.Split('\n');
        var indented = lines.Count(l => l.StartsWith("    ") || l.StartsWith('\t'));
        return hasCodeChars && (indented > 0 || t.Contains(';')) ? "code" : null;
    }
}
