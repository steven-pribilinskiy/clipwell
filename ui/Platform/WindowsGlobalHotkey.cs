using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;

namespace Clipwell.Ui.Platform;

/// <summary>
/// Windows global hotkey via <c>RegisterHotKey</c> on a dedicated message-pump
/// thread. Default chord is Alt+Shift+V (Win+V is reserved by the OS clipboard).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsGlobalHotkey : IGlobalHotkey
{
    private const int WM_HOTKEY = 0x0312;
    private const int WM_DESTROY = 0x0002;
    private const int WM_REBIND = 0x8000; // WM_APP — re-register the current chord
    private const uint MOD_NOREPEAT = 0x4000;
    private const int HotkeyId = 0xC110;
    private static readonly IntPtr HWND_MESSAGE = new(-3);

    public event Action? Pressed;

    private Thread? _thread;
    private IntPtr _hwnd;
    private WndProc? _wndProc;
    private volatile bool _started;
    private volatile HotkeyChord _chord = HotkeyChord.Default;

    public bool Register(HotkeyChord chord)
    {
        _chord = chord;
        if (_started) return true;
        _started = true;
        _thread = new Thread(Run) { IsBackground = true, Name = "clipwell-hotkey" };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        return true;
    }

    public bool Rebind(HotkeyChord chord)
    {
        _chord = chord;
        if (_hwnd != IntPtr.Zero) PostMessage(_hwnd, WM_REBIND, IntPtr.Zero, IntPtr.Zero);
        return true;
    }

    private void Apply()
    {
        UnregisterHotKey(_hwnd, HotkeyId);
        var c = _chord;
        RegisterHotKey(_hwnd, HotkeyId, c.WinModifiers() | MOD_NOREPEAT, c.WinVk());
    }

    private void Run()
    {
        _wndProc = WindowProc;
        var className = "ClipwellHotkey";
        var wc = new WNDCLASS
        {
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
            lpszClassName = className,
            hInstance = GetModuleHandle(null),
        };
        RegisterClass(ref wc);
        _hwnd = CreateWindowEx(0, className, "Clipwell", 0, 0, 0, 0, 0,
            HWND_MESSAGE, IntPtr.Zero, wc.hInstance, IntPtr.Zero);
        if (_hwnd == IntPtr.Zero) return;

        Apply();

        while (GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            TranslateMessage(ref msg);
            DispatchMessage(ref msg); // routes WM_HOTKEY to WindowProc
        }
    }

    private IntPtr WindowProc(IntPtr h, uint msg, IntPtr w, IntPtr l)
    {
        if (msg == WM_HOTKEY && (int)w == HotkeyId) { Pressed?.Invoke(); return IntPtr.Zero; }
        if (msg == WM_REBIND) { Apply(); return IntPtr.Zero; }
        if (msg == WM_DESTROY) UnregisterHotKey(h, HotkeyId);
        return DefWindowProc(h, msg, w, l);
    }

    public void Dispose()
    {
        if (_hwnd != IntPtr.Zero)
        {
            PostMessage(_hwnd, WM_DESTROY, IntPtr.Zero, IntPtr.Zero);
            _hwnd = IntPtr.Zero;
        }
    }

    private delegate IntPtr WndProc(IntPtr h, uint m, IntPtr w, IntPtr l);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASS
    {
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra, cbWndExtra;
        public IntPtr hInstance, hIcon, hCursor, hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam, lParam;
        public uint time;
        public int ptX, ptY;
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClass(ref WNDCLASS c);
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowEx(uint exStyle, string cls, string name, uint style,
        int x, int y, int w, int h, IntPtr parent, IntPtr menu, IntPtr inst, IntPtr param);
    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProc(IntPtr h, uint m, IntPtr w, IntPtr l);
    [DllImport("user32.dll")]
    private static extern int GetMessage(out MSG m, IntPtr h, uint min, uint max);
    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG m);
    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref MSG m);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr h, int id, uint mods, uint vk);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr h, int id);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? name);
}
