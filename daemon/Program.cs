using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Clipwell.Daemon;
using Clipwell.Daemon.Mcp;
using Clipwell.Daemon.Windows;
using Clipwell.Protocol;

var builder = WebApplication.CreateBuilder(args);

// Clipwell's own default port (8787), overridable via CLIPWELL_URL. Avoids the
// crowded 5000/5001 range that dev servers squat on.
builder.WebHost.UseUrls(Environment.GetEnvironmentVariable("CLIPWELL_URL") ?? "http://127.0.0.1:8787");

builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});

builder.Services.AddOpenApi("v1");

builder.Services.AddSingleton<MetadataStore>();
builder.Services.AddSingleton<HistoryStore>();
builder.Services.AddSingleton<ClipboardHub>();
builder.Services.AddSingleton<IClipboardWatcher>(sp =>
    ClipboardWatcherFactory.Create(sp.GetRequiredService<HistoryStore>().CacheDir));

// MCP over HTTP/SSE, served in-process at /mcp (tools hit HistoryStore directly).
// The stdio server in mcp/ remains for clients that spawn a child process.
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithTools<DaemonClipboardTools>();

var app = builder.Build();
app.UseWebSockets();
app.MapOpenApi("/openapi/v1.json"); // machine-readable API spec (feeds the docs site)
app.MapMcp("/mcp"); // Streamable HTTP + SSE MCP endpoint

var store = app.Services.GetRequiredService<HistoryStore>();
var meta = app.Services.GetRequiredService<MetadataStore>();
var hub = app.Services.GetRequiredService<ClipboardHub>();
var watcher = app.Services.GetRequiredService<IClipboardWatcher>();

app.Logger.LogInformation(
    "CLIPWELL_DATA_DIR env = {Env}; resolved db = {Db}",
    Environment.GetEnvironmentVariable("CLIPWELL_DATA_DIR") ?? "(null)", store.DbPath);
var jsonOpts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

if (OperatingSystem.IsWindows() && watcher is WindowsClipboardWatcher win)
    win.Failed += msg => app.Logger.LogError("clipboard watcher init failed: {Message}", msg);

// Capture pipeline: clipboard change → persist → broadcast to live subscribers.
watcher.Changed += row =>
{
    try
    {
        var isNew = store.Upsert(row);
        if (!isNew) return;
        app.Logger.LogInformation("captured clipboard item ({Len} chars)", row.TextLength);
        var payload = JsonSerializer.Serialize(
            new { type = "clipboard.changed", timestamp = row.Timestamp, textLength = row.TextLength },
            jsonOpts);
        hub.Broadcast(payload);
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "capture pipeline failed");
    }
};
watcher.Start();

// Hourly retention sweep. The delay comes first so a short-lived run (e.g. a dev
// session killed within minutes) never triggers a destructive purge — only a
// daemon that stays up for an hour actually sweeps. Set CLIPWELL_NO_SWEEP to
// disable purging entirely (useful when pointed at a shared/real DB).
var sweepDisabled = Environment.GetEnvironmentVariable("CLIPWELL_NO_SWEEP") is not null;
_ = Task.Run(async () =>
{
    while (!sweepDisabled)
    {
        await Task.Delay(TimeSpan.FromHours(1));
        try
        {
            var deleted = store.SweepOlderThan(store.LoadSettings().RetentionDays);
            if (deleted > 0) app.Logger.LogInformation("retention sweep purged {Count} rows", deleted);
        }
        catch (Exception ex)
        {
            app.Logger.LogError(ex, "retention sweep failed");
        }
    }
});

app.Lifetime.ApplicationStopping.Register(() => watcher.Dispose());

// ── REST ────────────────────────────────────────────────────────────────

app.MapGet("/health", () => Results.Ok(new { status = "ok", db = store.DbPath, subscribers = hub.SubscriberCount }))
    .WithName("Health").WithSummary("Liveness probe and basic daemon info.");

app.MapGet("/api/clipboard", (int? limit, string? before) =>
{
    var items = store.QueryPage(Math.Clamp(limit ?? 200, 1, 1000), before);
    return Results.Ok(new { items });
})
    .WithName("GetHistory")
    .WithSummary("List clipboard history, newest first. Page back with `before` (a timestamp).");

app.MapGet("/api/clipboard/settings", () => Results.Ok(store.LoadSettings()))
    .WithName("GetSettings").WithSummary("Get daemon settings (retention).");

app.MapPost("/api/clipboard/settings", (ClipboardSettings settings) =>
{
    store.SaveSettings(settings);
    return Results.Ok(settings);
})
    .WithName("SaveSettings").WithSummary("Update daemon settings (retention).");

app.MapPost("/api/clipboard/delete", (DeleteRequest req) =>
    Results.Ok(new { deleted = store.DeleteByTimestamp(req.Timestamp) }))
    .WithName("DeleteItem").WithSummary("Delete one history item by its timestamp.");

app.MapPost("/api/clipboard/clear", () => Results.Ok(new { deleted = store.ClearAll() }))
    .WithName("ClearHistory").WithSummary("Delete all history.");

app.MapPost("/api/clipboard/pin", (PinRequest req) =>
{
    if (string.IsNullOrEmpty(req.Timestamp)) return Results.BadRequest(new { error = "timestamp required" });
    meta.SetPinned(req.Timestamp, req.Pinned);
    return Results.Ok(new { req.Timestamp, req.Pinned });
})
    .WithName("PinItem").WithSummary("Pin or unpin an item (kept across retention).");

app.MapPost("/api/clipboard/sensitive", (SensitiveRequest req) =>
{
    if (string.IsNullOrEmpty(req.Timestamp)) return Results.BadRequest(new { error = "timestamp required" });
    meta.SetSensitive(req.Timestamp, req.Sensitive);
    return Results.Ok(new { req.Timestamp, req.Sensitive });
})
    .WithName("MarkSensitive").WithSummary("Mark or unmark an item as sensitive (masked in the UI).");

app.MapPost("/api/clipboard/rename", (RenameRequest req) =>
{
    if (string.IsNullOrEmpty(req.Timestamp)) return Results.BadRequest(new { error = "timestamp required" });
    meta.SetAlias(req.Timestamp, req.Alias);
    return Results.Ok(new { req.Timestamp, req.Alias });
})
    .WithName("RenameItem").WithSummary("Set or clear a custom alias for an item.");

app.MapGet("/api/clipboard/image/{timestamp}", (string timestamp) =>
{
    var path = store.GetImagePath(timestamp);
    return path is not null && File.Exists(path)
        ? Results.File(path, "image/png")
        : Results.NotFound();
})
    .WithName("GetImage").WithSummary("Fetch the cached PNG for an image item, by timestamp.");

// ── SSE ─────────────────────────────────────────────────────────────────

app.MapGet("/api/clipboard/stream", async (HttpContext ctx) =>
{
    ctx.Response.Headers.ContentType = "text/event-stream";
    ctx.Response.Headers.CacheControl = "no-cache";
    var (id, reader) = hub.Subscribe();
    try
    {
        await foreach (var payload in reader.ReadAllAsync(ctx.RequestAborted))
        {
            await ctx.Response.WriteAsync($"data: {payload}\n\n", ctx.RequestAborted);
            await ctx.Response.Body.FlushAsync(ctx.RequestAborted);
        }
    }
    catch (OperationCanceledException)
    {
        // client disconnected
    }
    finally
    {
        hub.Unsubscribe(id);
    }
})
    .WithName("StreamSse").WithSummary("Server-Sent Events stream of clipboard.changed events.");

// ── WebSocket ─────────────────────────────────────────────────────────────

app.MapGet("/api/clipboard/ws", async (HttpContext ctx) =>
{
    if (!ctx.WebSockets.IsWebSocketRequest)
    {
        ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }
    using var socket = await ctx.WebSockets.AcceptWebSocketAsync();
    var (id, reader) = hub.Subscribe();
    try
    {
        await foreach (var payload in reader.ReadAllAsync(ctx.RequestAborted))
        {
            var bytes = Encoding.UTF8.GetBytes(payload);
            await socket.SendAsync(bytes, WebSocketMessageType.Text, true, ctx.RequestAborted);
        }
    }
    catch (OperationCanceledException)
    {
        // client disconnected
    }
    finally
    {
        hub.Unsubscribe(id);
    }
});

app.Run();

internal sealed record DeleteRequest(string Timestamp);
internal sealed record PinRequest(string Timestamp, bool Pinned);
internal sealed record SensitiveRequest(string Timestamp, bool Sensitive);
internal sealed record RenameRequest(string Timestamp, string? Alias);
