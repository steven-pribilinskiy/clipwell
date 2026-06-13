namespace Clipwell.Protocol;

/// <summary>
/// User-tunable daemon settings. <see cref="RetentionDays"/> of <c>null</c> means
/// keep history forever.
/// </summary>
public sealed record ClipboardSettings
{
    public int? RetentionDays { get; init; } = 30;

    /// <summary>Retention values the UI offers; anything else falls back to the default.</summary>
    public static readonly int?[] ValidRetentions = [7, 30, 90, null];
}
