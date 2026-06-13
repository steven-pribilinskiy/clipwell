using System.Net.Http.Json;
using Clipwell.Protocol;

namespace Clipwell.Mcp;

/// <summary>HTTP client for the Clipwell daemon's REST API.</summary>
public sealed class DaemonClient
{
    private readonly HttpClient _http;

    public DaemonClient()
    {
        var baseUrl = Environment.GetEnvironmentVariable("CLIPWELL_API") ?? "http://127.0.0.1:8787";
        _http = new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(5) };
    }

    private sealed record PageResponse(List<ClipItem> Items);

    public async Task<IReadOnlyList<ClipItem>> GetPageAsync(int limit, string? before = null)
    {
        var url = before is null
            ? $"/api/clipboard?limit={limit}"
            : $"/api/clipboard?limit={limit}&before={Uri.EscapeDataString(before)}";
        var page = await _http.GetFromJsonAsync<PageResponse>(url);
        return page?.Items ?? [];
    }

    public async Task<int> ClearAsync()
    {
        var res = await _http.PostAsync("/api/clipboard/clear", null);
        res.EnsureSuccessStatusCode();
        return 1;
    }

    public async Task<bool> DeleteAsync(string timestamp)
    {
        var res = await _http.PostAsJsonAsync("/api/clipboard/delete", new { timestamp });
        return res.IsSuccessStatusCode;
    }
}
