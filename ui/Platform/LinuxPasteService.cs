using System;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Threading;

namespace Clipwell.Ui.Platform;

/// <summary>
/// Linux paste service. On Linux, hiding our window returns focus to the previously
/// active app, so we just synthesize the paste shortcut there — no window handle to
/// capture. Uses <c>xdotool</c> (X11) or <c>wtype</c> (Wayland) if installed.
/// </summary>
/// <remarks>
/// Compile-checked in CI; runtime behavior pending verification on real hardware.
/// If neither tool is present, the item is still copied — the user pastes manually.
/// </remarks>
[SupportedOSPlatform("linux")]
public sealed class LinuxPasteService : IPasteService
{
    // No per-window targeting on Linux; the focused app receives the keystroke.
    public nint GetForegroundWindow() => 0;

    public void PasteInto(nint target)
    {
        Thread.Sleep(60); // let focus return to the previous app after Hide()
        if (TryRun("xdotool", "key --clearmodifiers ctrl+v")) return;
        TryRun("wtype", "-M ctrl v -m ctrl"); // Wayland fallback
    }

    private static bool TryRun(string file, string args)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo
            {
                FileName = file,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            });
            if (p is null) return false;
            p.WaitForExit(2000);
            return p.ExitCode == 0;
        }
        catch
        {
            return false; // tool not installed
        }
    }
}
