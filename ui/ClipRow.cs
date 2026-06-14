using System;
using System.Globalization;
using Clipwell.Protocol;

namespace Clipwell.Ui;

/// <summary>
/// View-side wrapper around a <see cref="ClipItem"/> that exposes display-ready
/// strings, keeping the XAML free of converters.
/// </summary>
public sealed class ClipRow(ClipItem item)
{
    public ClipItem Item { get; } = item;

    public string Preview
    {
        get
        {
            var text = Item.TextContent;
            if (string.IsNullOrEmpty(text))
                return Item.HasImage ? "🖼  image" : "(empty)";
            var oneLine = text.ReplaceLineEndings(" ").Trim();
            return oneLine.Length > 200 ? oneLine[..200] + "…" : oneLine;
        }
    }

    public string KindGlyph => Item.Kind switch
    {
        "url" => "🔗",
        "email" => "✉",
        "color" => "🎨",
        "path" => "📁",
        "code" => "{ }",
        "image" => "🖼",
        _ => "📄",
    };

    public string Meta
    {
        get
        {
            var when = FormatWhen(Item.Timestamp);
            return string.IsNullOrEmpty(Item.SourceApp) ? when : $"{Item.SourceApp} · {when}";
        }
    }

    private static string FormatWhen(string timestamp)
    {
        if (!DateTimeOffset.TryParse(timestamp, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out var ts))
            return timestamp;
        var delta = DateTimeOffset.Now - ts;
        if (delta < TimeSpan.FromMinutes(1)) return "just now";
        if (delta < TimeSpan.FromHours(1)) return $"{(int)delta.TotalMinutes}m ago";
        if (delta < TimeSpan.FromDays(1)) return $"{(int)delta.TotalHours}h ago";
        if (delta < TimeSpan.FromDays(7)) return $"{(int)delta.TotalDays}d ago";
        return ts.LocalDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}
