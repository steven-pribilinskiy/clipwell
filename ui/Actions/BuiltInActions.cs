using System.Threading.Tasks;
using Clipwell.Protocol;
using Clipwell.Protocol.Plugins;

namespace Clipwell.Ui.Actions;

/// <summary>Open a URL item (or GitHub PR) in the browser.</summary>
public sealed class OpenUrlAction : IClipAction
{
    public string Id => "builtin.open-url";
    public string Label => "Open in browser";
    public bool AppliesTo(ClipItem item) => item.Kind is "url" or "github-pr";
    public Task ExecuteAsync(ClipItem item, IClipActionContext ctx)
    {
        if (!string.IsNullOrWhiteSpace(item.TextContent)) ctx.OpenUrl(item.TextContent.Trim());
        return Task.CompletedTask;
    }
}

/// <summary>Open a path item with its default handler.</summary>
public sealed class OpenPathAction : IClipAction
{
    public string Id => "builtin.open-path";
    public string Label => "Open file or folder";
    public bool AppliesTo(ClipItem item) => item.Kind is "path";
    public Task ExecuteAsync(ClipItem item, IClipActionContext ctx)
    {
        if (!string.IsNullOrWhiteSpace(item.TextContent)) ctx.OpenPath(item.TextContent.Trim());
        return Task.CompletedTask;
    }
}

/// <summary>Copy the item's text to the clipboard (no paste).</summary>
public sealed class CopyTextAction : IClipAction
{
    public string Id => "builtin.copy";
    public string Label => "Copy to clipboard";
    public bool AppliesTo(ClipItem item) => !string.IsNullOrEmpty(item.TextContent) && !item.IsSensitive;
    public async Task ExecuteAsync(ClipItem item, IClipActionContext ctx)
    {
        if (!string.IsNullOrEmpty(item.TextContent)) await ctx.SetClipboardAsync(item.TextContent);
    }
}

/// <summary>Copy just the host of a URL (e.g. "github.com").</summary>
public sealed class CopyHostAction : IClipAction
{
    public string Id => "builtin.copy-host";
    public string Label => "Copy domain";
    public bool AppliesTo(ClipItem item) => item.Kind is "url" or "github-pr";
    public async Task ExecuteAsync(ClipItem item, IClipActionContext ctx)
    {
        if (System.Uri.TryCreate(item.TextContent?.Trim(), System.UriKind.Absolute, out var u))
            await ctx.SetClipboardAsync(u.Host);
    }
}
