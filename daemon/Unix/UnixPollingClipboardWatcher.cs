using System.Diagnostics;
using Clipwell.Protocol;

namespace Clipwell.Daemon.Unix;

public enum UnixClipboardTool
{
    MacOs,
    Linux,
}

/// <summary>
/// macOS / Linux clipboard watcher. Unix has no event for clipboard changes that
/// is portable across X11/Wayland, so this polls the system clipboard via the
/// platform CLI (<c>pbpaste</c> on macOS; <c>wl-paste</c> or <c>xclip</c> on
/// Linux) and emits when the text changes.
/// </summary>
/// <remarks>
/// Text-only for the first cut. Requires the relevant CLI to be installed
/// (pbpaste ships with macOS; Linux needs wl-clipboard or xclip). Implemented but
/// not yet exercised in CI — see ADR / Phase 1 notes.
/// </remarks>
public sealed class UnixPollingClipboardWatcher(UnixClipboardTool tool) : IClipboardWatcher
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(600);

    public event Action<StoreRow>? Changed;
    public event Action<string>? Failed;

    private CancellationTokenSource? _cts;
    private string? _last;

    public void Start()
    {
        if (_cts is not null) return;
        _cts = new CancellationTokenSource();
        _ = Task.Run(() => PollLoopAsync(_cts.Token));
    }

    private async Task PollLoopAsync(CancellationToken ct)
    {
        // Seed with the current content so we don't emit a spurious "change" for
        // whatever was already on the clipboard at startup.
        _last = ReadClipboardText();
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(PollInterval, ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            string? text;
            try
            {
                text = ReadClipboardText();
            }
            catch (Exception ex)
            {
                Failed?.Invoke($"clipboard read failed: {ex.Message}");
                continue;
            }

            if (string.IsNullOrEmpty(text) || text == _last) continue;
            _last = text;
            Changed?.Invoke(new StoreRow
            {
                Timestamp = DateTimeOffset.UtcNow.ToString("o"),
                TextContent = text,
                TextLength = text.Length,
                HasImage = false,
                Formats = ["text"],
            });
        }
    }

    private string? ReadClipboardText() => tool switch
    {
        UnixClipboardTool.MacOs => Run("pbpaste", ""),
        UnixClipboardTool.Linux => RunLinux(),
        _ => null,
    };

    private static string? RunLinux()
    {
        // Prefer Wayland's wl-paste; fall back to X11's xclip.
        return Run("wl-paste", "--no-newline") ?? Run("xclip", "-selection clipboard -o");
    }

    private static string? Run(string file, string args)
    {
        try
        {
            using var proc = Process.Start(new ProcessStartInfo
            {
                FileName = file,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            if (proc is null) return null;
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(2000);
            return proc.ExitCode == 0 ? output : null;
        }
        catch
        {
            // Tool not installed / not on PATH.
            return null;
        }
    }

    public void Dispose() => _cts?.Cancel();
}
