namespace Clipwell.Protocol.Plugins;

/// <summary>
/// Classifies a captured clipboard item into a typed <c>Kind</c> (e.g. url, image,
/// code, claude-session). Detectors are the public extension seam: built-in
/// detectors ship in the core; personal/workflow detectors load as plugins.
/// </summary>
/// <remarks>
/// Phase 2 fleshes this out. The contract is declared now so the daemon's pipeline
/// (capture → detect → store → broadcast) is shaped around plugins from day one.
/// </remarks>
public interface IClipDetector
{
    /// <summary>Stable id, e.g. <c>builtin.url</c>.</summary>
    string Id { get; }

    /// <summary>Lower number wins when multiple detectors match.</summary>
    int Priority { get; }

    /// <summary>Returns the <c>Kind</c> to assign, or null if this detector does not match.</summary>
    string? Detect(ClipItem item);
}
