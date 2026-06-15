using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;

namespace Clipwell.Ui;

/// <summary>
/// Best-effort favicon fetcher for URL items. Fetches <c>https://&lt;host&gt;/favicon.ico</c>
/// directly (HTTPS only — no third-party favicon service, to avoid leaking the
/// user's domains), decodes it, and caches per host. Returns null on any failure;
/// callers fall back to the kind glyph.
/// </summary>
public static class FaviconLoader
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(4) };
    private static readonly Dictionary<string, Bitmap?> Cache = new();
    private static readonly object Gate = new();

    public static async Task<Bitmap?> GetAsync(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            return null;

        var host = uri.Host;
        lock (Gate)
        {
            if (Cache.TryGetValue(host, out var cached)) return cached; // includes failed (null)
        }

        Bitmap? bmp = null;
        try
        {
            var bytes = await Http.GetByteArrayAsync($"https://{host}/favicon.ico");
            using var ms = new MemoryStream(bytes);
            bmp = Bitmap.DecodeToWidth(ms, 32); // may throw on .ico Skia can't decode
        }
        catch
        {
            bmp = null; // unreachable host / undecodable icon → glyph fallback
        }

        lock (Gate)
        {
            if (Cache.Count > 200) Cache.Clear(); // crude bound
            Cache[host] = bmp;
        }
        return bmp;
    }
}
