using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Avalonia;

namespace Clipwell.Ui.Platform;

[SupportedOSPlatform("windows")]
public sealed class WindowsPointerLocation : IPointerLocation
{
    public bool TryGetCursor(out PixelPoint point)
    {
        // The app is per-monitor DPI aware, so GetCursorPos returns physical pixels.
        if (GetCursorPos(out var p))
        {
            point = new PixelPoint(p.X, p.Y);
            return true;
        }
        point = default;
        return false;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);
}
