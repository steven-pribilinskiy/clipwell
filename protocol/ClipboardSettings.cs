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

    // ── UI preferences (persisted here for one settings surface; the daemon just
    //    stores them, the picker applies them) ────────────────────────────────
    /// <summary>"system" (default), "light", or "dark".</summary>
    public string Theme { get; init; } = "system";

    /// <summary>Default picker view: "compact" (default) or "detail".</summary>
    public string DefaultView { get; init; } = "compact";

    /// <summary>Default grouping: "none" (default), "date", or "source".</summary>
    public string DefaultGroup { get; init; } = "none";

    /// <summary>Show the source app in each row's metadata line.</summary>
    public bool ShowSource { get; init; } = true;

    /// <summary>Show the relative time in each row's metadata line.</summary>
    public bool ShowTime { get; init; } = true;

    /// <summary>Retention values the UI offers; anything else falls back to the default.</summary>
    public static readonly int?[] ValidRetentions = [7, 30, 90, null];
}
