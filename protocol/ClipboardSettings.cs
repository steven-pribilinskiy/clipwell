namespace Clipwell.Protocol;

/// <summary>
/// User-tunable daemon settings. <see cref="RetentionDays"/> of <c>null</c> means
/// keep history forever.
/// </summary>
public sealed record ClipboardSettings
{
    public int? RetentionDays { get; init; } = 30;

    /// <summary>
    /// When true, the picker opens at the mouse cursor (clamped to the screen)
    /// instead of centered. Default false (centered).
    /// </summary>
    public bool OpenAtCursor { get; init; }

    /// <summary>Retention values the UI offers; anything else falls back to the default.</summary>
    public static readonly int?[] ValidRetentions = [7, 30, 90, null];
}
