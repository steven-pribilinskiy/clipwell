using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;

namespace Clipwell.Ui.Platform;

/// <summary>
/// Linux (X11) global hotkey via <c>XGrabKey</c> on the root window, with a
/// dedicated thread running an <c>XNextEvent</c> loop. Default chord is
/// Alt+Shift+V, to match the other platforms.
/// </summary>
/// <remarks>
/// X11 only — under a pure Wayland session there is no global key-grab API
/// (compositors expose hotkeys through portals instead), so this returns false
/// and the app falls back to tray-only. Implemented and compile-checked in CI;
/// runtime behavior is pending verification on real Linux hardware.
/// </remarks>
[SupportedOSPlatform("linux")]
public sealed class LinuxGlobalHotkey : IGlobalHotkey
{
    // X11 lock modifiers (combined with the chord's modifiers when grabbing).
    private const uint LockMask = 0x02, Mod2Mask = 0x10;
    private const int KeyPress = 2;
    private const int GrabModeAsync = 1;

    public event Action? Pressed;

    private IntPtr _display;
    private ulong _root;
    private byte _keycode;
    private uint _mods;
    private Thread? _thread;
    private volatile bool _running;

    // Live rebind on X11 would need ungrab/regrab on the event thread; for now the
    // new chord applies on next launch.
    public bool Rebind(HotkeyChord chord) => false;

    public bool Register(HotkeyChord chord)
    {
        try
        {
            _display = XOpenDisplay(IntPtr.Zero);
            if (_display == IntPtr.Zero) return false; // no X server (e.g. headless / Wayland-only)

            _root = XDefaultRootWindow(_display);
            _keycode = XKeysymToKeycode(_display, chord.X11Keysym());
            if (_keycode == 0) return false;

            var baseMods = _mods = chord.X11Modifiers();
            // Grab the chord with every combination of the "lock" modifiers (Caps/Num)
            // so it fires regardless of their state.
            foreach (var extra in new[] { 0u, LockMask, Mod2Mask, LockMask | Mod2Mask })
                XGrabKey(_display, _keycode, baseMods | extra, _root, false, GrabModeAsync, GrabModeAsync);

            XSelectInput(_display, _root, 1L << 0 /* KeyPressMask */);
            _running = true;
            _thread = new Thread(EventLoop) { IsBackground = true, Name = "clipwell-hotkey-x11" };
            _thread.Start();
            return true;
        }
        catch
        {
            return false; // libX11 missing or any interop failure → tray-only
        }
    }

    private void EventLoop()
    {
        // XEvent is a large union; we only need the leading 'type' int because the
        // only key we grabbed is our chord.
        var ev = Marshal.AllocHGlobal(256);
        try
        {
            while (_running)
            {
                XNextEvent(_display, ev); // blocks until an event we grabbed arrives
                if (!_running) break;
                if (Marshal.ReadInt32(ev) == KeyPress)
                    Pressed?.Invoke();
            }
        }
        catch
        {
            // display closed / interop error — stop quietly
        }
        finally
        {
            Marshal.FreeHGlobal(ev);
        }
    }

    public void Dispose()
    {
        _running = false;
        try
        {
            if (_display != IntPtr.Zero)
            {
                XUngrabKey(_display, _keycode, _mods, _root);
                XCloseDisplay(_display);
                _display = IntPtr.Zero;
            }
        }
        catch { /* best effort */ }
    }

    private const string X11 = "libX11.so.6";
    [DllImport(X11)] private static extern IntPtr XOpenDisplay(IntPtr display);
    [DllImport(X11)] private static extern int XCloseDisplay(IntPtr display);
    [DllImport(X11)] private static extern ulong XDefaultRootWindow(IntPtr display);
    [DllImport(X11)] private static extern byte XKeysymToKeycode(IntPtr display, ulong keysym);
    [DllImport(X11)] private static extern int XGrabKey(IntPtr display, int keycode, uint modifiers, ulong grabWindow, bool ownerEvents, int pointerMode, int keyboardMode);
    [DllImport(X11)] private static extern int XUngrabKey(IntPtr display, int keycode, uint modifiers, ulong grabWindow);
    [DllImport(X11)] private static extern int XSelectInput(IntPtr display, ulong window, long mask);
    [DllImport(X11)] private static extern int XNextEvent(IntPtr display, IntPtr eventReturn);
}
