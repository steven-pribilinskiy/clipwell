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
        if (OperatingSystem.IsWindows())
            return new WindowsClipboardWatcher(cacheDir);
        if (OperatingSystem.IsMacOS())
            return new UnixPollingClipboardWatcher(UnixClipboardTool.MacOs);
        if (OperatingSystem.IsLinux())
            return new UnixPollingClipboardWatcher(UnixClipboardTool.Linux);
        return new NullClipboardWatcher();
    }
}
