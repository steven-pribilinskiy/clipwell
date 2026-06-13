using System.ComponentModel;
using System.Text;
using Clipwell.Protocol;
using ModelContextProtocol.Server;

namespace Clipwell.Mcp;

/// <summary>
/// MCP tools exposing the user's clipboard history to AI agents. Each tool proxies
/// to the daemon's REST API.
/// </summary>
[McpServerToolType]
public sealed class ClipboardTools(DaemonClient daemon)
{
    [McpServerTool(Name = "clipboard_recent")]
    [Description("List the most recent clipboard history items, newest first. Returns timestamp, kind, source app, and a text preview for each.")]
    public async Task<string> Recent(
        [Description("Maximum number of items to return (1-200).")] int limit = 20)
    {
        var items = await daemon.GetPageAsync(Math.Clamp(limit, 1, 200));
        return Format(items);
    }

    [McpServerTool(Name = "clipboard_search")]
    [Description("Search clipboard history for items whose text contains the query (case-insensitive). Returns matching items newest first.")]
    public async Task<string> Search(
        [Description("Text to search for within clipboard items.")] string query,
        [Description("Maximum number of matches to return (1-200).")] int limit = 50)
    {
        // Pull a generous window and filter locally; the daemon paginates by time.
        var items = await daemon.GetPageAsync(500);
        var matches = items
            .Where(i => i.TextContent?.Contains(query, StringComparison.OrdinalIgnoreCase) == true)
            .Take(Math.Clamp(limit, 1, 200))
            .ToList();
        return matches.Count == 0 ? $"No clipboard items matching \"{query}\"." : Format(matches);
    }

    [McpServerTool(Name = "clipboard_get_text")]
    [Description("Get the full, untruncated text of a clipboard item by its exact timestamp (as returned by clipboard_recent/clipboard_search).")]
    public async Task<string> GetText(
        [Description("The item's timestamp, e.g. 2026-06-14T01:23:45.6789012+00:00.")] string timestamp)
    {
        var items = await daemon.GetPageAsync(500);
        var item = items.FirstOrDefault(i => i.Timestamp == timestamp);
        if (item is null) return "No clipboard item with that timestamp.";
        return item.TextContent ?? (item.HasImage ? "(image item — no text)" : "(empty)");
    }

    [McpServerTool(Name = "clipboard_clear")]
    [Description("Delete ALL clipboard history. This is irreversible.")]
    public async Task<string> Clear()
    {
        await daemon.ClearAsync();
        return "Clipboard history cleared.";
    }

    private static string Format(IReadOnlyList<ClipItem> items)
    {
        var sb = new StringBuilder();
        foreach (var i in items)
        {
            var preview = (i.TextContent ?? (i.HasImage ? "<image>" : "<empty>"))
                .ReplaceLineEndings(" ").Trim();
            if (preview.Length > 120) preview = preview[..120] + "…";
            var src = string.IsNullOrEmpty(i.SourceApp) ? "" : $" [{i.SourceApp}]";
            sb.AppendLine($"{i.Timestamp}{src}: {preview}");
        }
        return sb.ToString().TrimEnd();
    }
}
