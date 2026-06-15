using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Clipwell.Protocol;

namespace Clipwell.Ui;

/// <summary>
/// Thin client for the Clipwell daemon's public API. The UI is just another API
/// consumer — same REST + WebSocket surface any third party would use.
/// </summary>
public sealed class ClipwellClient
{
    private readonly Uri _baseUri;
    private readonly HttpClient _http;

    public ClipwellClient()
    {
        var baseUrl = Environment.GetEnvironmentVariable("CLIPWELL_API") ?? "http://127.0.0.1:8787";
        _baseUri = new Uri(baseUrl);
        _http = new HttpClient { BaseAddress = _baseUri, Timeout = TimeSpan.FromSeconds(5) };
    }

    private sealed record PageResponse(List<ClipItem> Items);

    public sealed record HealthInfo(string? Status, string? Db, int Subscribers);

    public async Task<HealthInfo?> GetHealthAsync()
    {
        try { return await _http.GetFromJsonAsync<HealthInfo>("/health"); }
        catch { return null; }
    }

    public async Task<IReadOnlyList<ClipItem>> GetPageAsync(int limit = 200, string? before = null)
    {
        var url = before is null
            ? $"/api/clipboard?limit={limit}"
            : $"/api/clipboard?limit={limit}&before={Uri.EscapeDataString(before)}";
        var page = await _http.GetFromJsonAsync<PageResponse>(url);
        return page?.Items ?? [];
    }

    public async Task<ClipboardSettings> GetSettingsAsync()
    {
        try { return await _http.GetFromJsonAsync<ClipboardSettings>("/api/clipboard/settings") ?? new(); }
        catch { return new ClipboardSettings(); }
    }

    public async Task SaveSettingsAsync(ClipboardSettings settings) =>
        await _http.PostAsJsonAsync("/api/clipboard/settings", settings);

    public async Task DeleteAsync(string timestamp) =>
        await _http.PostAsJsonAsync("/api/clipboard/delete", new { timestamp });

    public async Task PinAsync(string timestamp, bool pinned) =>
        await _http.PostAsJsonAsync("/api/clipboard/pin", new { timestamp, pinned });

    public async Task SensitiveAsync(string timestamp, bool sensitive) =>
        await _http.PostAsJsonAsync("/api/clipboard/sensitive", new { timestamp, sensitive });

    public async Task RenameAsync(string timestamp, string? alias) =>
        await _http.PostAsJsonAsync("/api/clipboard/rename", new { timestamp, alias });

    /// <summary>Absolute URL to an item's cached image (for thumbnails).</summary>
    public string ImageUrl(string timestamp) =>
        new Uri(_baseUri, $"/api/clipboard/image/{Uri.EscapeDataString(timestamp)}").ToString();

    /// <summary>Fetch an item's cached image bytes, or null if none.</summary>
    public async Task<byte[]?> GetImageBytesAsync(string timestamp)
    {
        try
        {
            var res = await _http.GetAsync($"/api/clipboard/image/{Uri.EscapeDataString(timestamp)}");
            return res.IsSuccessStatusCode ? await res.Content.ReadAsByteArrayAsync() : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Connects to the daemon's WebSocket and invokes <paramref name="onChange"/>
    /// each time the clipboard changes, until the token is cancelled. Reconnects
    /// with backoff if the daemon restarts.
    /// </summary>
    public async Task ListenAsync(Action onChange, CancellationToken ct)
    {
        var wsUri = new Uri($"ws://{_baseUri.Host}:{_baseUri.Port}/api/clipboard/ws");
        var buffer = new byte[4096];
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var socket = new ClientWebSocket();
                await socket.ConnectAsync(wsUri, ct);
                while (socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
                {
                    var result = await socket.ReceiveAsync(buffer, ct);
                    if (result.MessageType == WebSocketMessageType.Close) break;
                    onChange();
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch
            {
                // Daemon down or restarting — wait and retry.
                try { await Task.Delay(TimeSpan.FromSeconds(2), ct); }
                catch (OperationCanceledException) { return; }
            }
        }
    }

    public bool IsLoopback => _baseUri.IsLoopback;
}
