using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Clipwell.Daemon;
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

builder.Services.AddSingleton<HistoryStore>();
builder.Services.AddSingleton<ClipboardHub>();
builder.Services.AddSingleton<IClipboardWatcher>(_ =>
    OperatingSystem.IsWindows() ? new WindowsClipboardWatcher() : new NullClipboardWatcher());

var app = builder.Build();
app.UseWebSockets();

var store = app.Services.GetRequiredService<HistoryStore>();
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
// daemon that stays up for an hour actually sweeps.
_ = Task.Run(async () =>
{
    while (true)
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

app.MapGet("/health", () => Results.Ok(new { status = "ok", db = store.DbPath, subscribers = hub.SubscriberCount }));

app.MapGet("/api/clipboard", (int? limit, string? before) =>
{
    var items = store.QueryPage(Math.Clamp(limit ?? 200, 1, 1000), before);
    return Results.Ok(new { items });
});

app.MapGet("/api/clipboard/settings", () => Results.Ok(store.LoadSettings()));

app.MapPost("/api/clipboard/settings", (ClipboardSettings settings) =>
{
    store.SaveSettings(settings);
    return Results.Ok(settings);
});

app.MapPost("/api/clipboard/delete", (DeleteRequest req) =>
    Results.Ok(new { deleted = store.DeleteByTimestamp(req.Timestamp) }));

app.MapPost("/api/clipboard/clear", () => Results.Ok(new { deleted = store.ClearAll() }));

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
});

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
