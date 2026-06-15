using Clipwell.Daemon.Unix;
using Clipwell.Daemon.Windows;

namespace Clipwell.Daemon;

/// <summary>
/// Picks the right clipboard watcher for the current OS. Each platform watcher
/// lives behind <see cref="IClipboardWatcher"/> so the rest of the daemon is
/// OS-agnostic.
/// </summary>
public static class ClipboardWatcherFactory
{
    public static IClipboardWatcher Create(string cacheDir)
    {
        // CLIPWELL_NO_WATCH=1 serves existing history without capturing new copies.
        // Used by the docs-capture scripts so the user's live clipboard activity
        // can't leak into screenshots/clips after the DB has been seeded.
        if (Environment.GetEnvironmentVariable("CLIPWELL_NO_WATCH") == "1")
            return new NullClipboardWatcher();
        if (OperatingSystem.IsWindows())
            return new WindowsClipboardWatcher(cacheDir);
        if (OperatingSystem.IsMacOS())
            return new UnixPollingClipboardWatcher(UnixClipboardTool.MacOs);
        if (OperatingSystem.IsLinux())
            return new UnixPollingClipboardWatcher(UnixClipboardTool.Linux);
        return new NullClipboardWatcher();
    }
}
