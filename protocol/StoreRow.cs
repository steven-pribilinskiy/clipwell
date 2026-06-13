namespace Clipwell.Protocol;

/// <summary>
/// Write-side representation of a clipboard capture, before it is persisted and
/// enriched into a <see cref="ClipItem"/>. Produced by clipboard watchers.
/// </summary>
public sealed record StoreRow
{
    public required string Timestamp { get; init; }
    public string? TextContent { get; init; }
    public int TextLength { get; init; }
    public string? HtmlContent { get; init; }
    public bool HasImage { get; init; }
    public string? ImagePath { get; init; }
    public string? SourceApp { get; init; }
    public IReadOnlyList<string> Formats { get; init; } = [];
}
