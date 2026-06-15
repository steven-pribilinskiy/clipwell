using Clipwell.Protocol;
using Clipwell.Protocol.Plugins;

namespace Clipwell.SamplePlugin;

/// <summary>
/// Sample plugin: classifies "TODO …" clipboard text as a custom <c>todo</c> kind.
/// Demonstrates that a plugin DLL dropped in the plugins dir extends the daemon's
/// detector registry without any core changes.
/// </summary>
public sealed class TodoDetector : IClipDetector
{
    public string Id => "sample.todo";
    public int Priority => 15; // before the generic URL/text detectors
    public string? Detect(ClipItem item) =>
        item.TextContent?.TrimStart().StartsWith("TODO", StringComparison.OrdinalIgnoreCase) == true
            ? "todo"
            : null;
}

/// <summary>
/// Sample action: copies the item's text upper-cased. Demonstrates a plugin action
/// appearing in the Ctrl+K palette.
/// </summary>
public sealed class ShoutAction : IClipAction
{
    public string Id => "sample.shout";
    public string Label => "Copy as SHOUTING";
    public bool AppliesTo(ClipItem item) => !string.IsNullOrEmpty(item.TextContent) && !item.IsSensitive;
    public async Task ExecuteAsync(ClipItem item, IClipActionContext ctx)
    {
        if (!string.IsNullOrEmpty(item.TextContent))
            await ctx.SetClipboardAsync(item.TextContent.ToUpperInvariant());
    }
}
