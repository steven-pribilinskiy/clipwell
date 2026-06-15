namespace Clipwell.Protocol.Plugins;

/// <summary>
/// A content-aware action offered for an item (e.g. "open PR", "copy transcript").
/// Like <see cref="IClipDetector"/>, actions are a plugin seam: generic actions ship
/// in the core, personal ones load as a private plugin.
/// </summary>
/// <remarks>Phase 2 fleshes this out; declared now to shape the API surface.</remarks>
public interface IClipAction
{
    /// <summary>Stable id, e.g. <c>builtin.open-with</c>.</summary>
    string Id { get; }

    /// <summary>Human-readable label for the action menu.</summary>
    string Label { get; }

    /// <summary>Whether this action applies to the given item.</summary>
    bool AppliesTo(ClipItem item);

    /// <summary>Run the action for the item, using host-provided services.</summary>
    Task ExecuteAsync(ClipItem item, IClipActionContext ctx);
}
