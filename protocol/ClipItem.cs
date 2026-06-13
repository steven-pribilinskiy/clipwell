namespace Clipwell.Protocol;

/// <summary>
/// A single clipboard-history entry as served over the public API. Mirrors the
/// shape the original windows-settings backend exposed so existing clients keep
/// working while the daemon takes over.
/// </summary>
public sealed record ClipItem
{
    /// <summary>Stable identifier for this item (e.g. <c>db:42</c>).</summary>
    public required string Id { get; init; }

    /// <summary>ISO-8601 capture time; also the primary sort/paging key.</summary>
    public required string Timestamp { get; init; }

    /// <summary>Clipboard formats present at capture (e.g. text, html, image).</summary>
    public IReadOnlyList<string> Formats { get; init; } = [];

    /// <summary>Detector-assigned type (e.g. <c>text</c>, <c>url</c>, <c>image</c>); null until classified.</summary>
    public string? Kind { get; init; }

    public string? TextContent { get; init; }

    public int TextLength { get; init; }

    public string? HtmlContent { get; init; }

    public bool HasImage { get; init; }

    /// <summary>True when the OS clipboard manager has this item pinned.</summary>
    public bool IsPinned { get; init; }

    /// <summary>True when the user pinned it within Clipwell.</summary>
    public bool IsUserPinned { get; init; }

    public bool IsSensitive { get; init; }

    public string SourceApp { get; init; } = "";

    public string? Alias { get; init; }

    public bool IsEdited { get; init; }
}
