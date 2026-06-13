using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Clipwell.Protocol;

namespace Clipwell.Daemon.Windows;

/// <summary>
/// Windows clipboard watcher. Creates a message-only window on a dedicated thread,
/// registers it as a clipboard-format listener, and pumps messages so that each
/// <c>WM_CLIPBOARDUPDATE</c> triggers a capture. Pure Win32 P/Invoke — no WPF or
/// WinForms dependency — so the daemon stays on the portable <c>net10.0</c> TFM.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsClipboardWatcher : IClipboardWatcher
{
    private const int WM_CLIPBOARDUPDATE = 0x031D;
    private const int WM_DESTROY = 0x0002;
    private const uint CF_UNICODETEXT = 13;
    private static readonly IntPtr HWND_MESSAGE = new(-3);

    public event Action<StoreRow>? Changed;

    /// <summary>Raised if the watcher cannot initialize (window/listener setup failed).</summary>
    public event Action<string>? Failed;

    private Thread? _thread;
    private IntPtr _hwnd;
    private WndProc? _wndProc; // kept alive against GC for the window's lifetime
    private volatile bool _started;

    public void Start()
    {
        if (_started) return;
        _started = true;
        _thread = new Thread(RunMessageLoop)
        {
            IsBackground = true,
            Name = "clipwell-clipboard-watcher",
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    private void RunMessageLoop()
    {
        try
        {
            RunMessageLoopCore();
        }
        catch (Exception ex)
        {
            // Never let a watcher-thread exception take down the whole daemon.
            Failed?.Invoke($"watcher thread crashed: {ex.Message}");
        }
    }

    private void RunMessageLoopCore()
    {
        _wndProc = WindowProc;
        var className = "ClipwellClipboardWatcher";
        var wndClass = new WNDCLASS
        {
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
            lpszClassName = className,
            hInstance = GetModuleHandle(null),
        };
        RegisterClass(ref wndClass);

        _hwnd = CreateWindowEx(
            0, className, "Clipwell", 0, 0, 0, 0, 0,
            HWND_MESSAGE, IntPtr.Zero, wndClass.hInstance, IntPtr.Zero);

        if (_hwnd == IntPtr.Zero)
        {
            Failed?.Invoke($"CreateWindowEx failed (error {Marshal.GetLastWin32Error()})");
            return;
        }
        if (!AddClipboardFormatListener(_hwnd))
            Failed?.Invoke($"AddClipboardFormatListener failed (error {Marshal.GetLastWin32Error()})");

        while (GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }
    }

    private IntPtr WindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_CLIPBOARDUPDATE)
        {
            try
            {
                CaptureCurrent();
            }
            catch
            {
                // A failed capture must never kill the message loop.
            }
            return IntPtr.Zero;
        }
        if (msg == WM_DESTROY)
        {
            RemoveClipboardFormatListener(hWnd);
        }
        return DefWindowProc(hWnd, msg, wParam, lParam);
    }

    private void CaptureCurrent()
    {
        var text = ReadUnicodeText();
        if (text is null) return; // non-text capture (image/other) handled in a later phase

        var now = DateTimeOffset.UtcNow.ToString("o");
        Changed?.Invoke(new StoreRow
        {
            Timestamp = now,
            TextContent = text,
            TextLength = text.Length,
            HasImage = false,
            Formats = ["text"],
        });
    }

    private static string? ReadUnicodeText()
    {
        if (!IsClipboardFormatAvailable(CF_UNICODETEXT)) return null;
        if (!OpenClipboard(IntPtr.Zero)) return null;
        try
        {
            var handle = GetClipboardData(CF_UNICODETEXT);
            if (handle == IntPtr.Zero) return null;
            var ptr = GlobalLock(handle);
            if (ptr == IntPtr.Zero) return null;
            try
            {
                return Marshal.PtrToStringUni(ptr);
            }
            finally
            {
                GlobalUnlock(handle);
            }
        }
        finally
        {
            CloseClipboard();
        }
    }

    public void Dispose()
    {
        if (_hwnd != IntPtr.Zero)
        {
            PostMessage(_hwnd, WM_DESTROY, IntPtr.Zero, IntPtr.Zero);
            _hwnd = IntPtr.Zero;
        }
    }

    // ── P/Invoke ────────────────────────────────────────────────────────

    private delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASS
    {
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int ptX;
        public int ptY;
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClass(ref WNDCLASS lpWndClass);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowEx(
        uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle,
        int x, int y, int nWidth, int nHeight,
        IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint min, uint max);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref MSG lpMsg);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetClipboardData(uint uFormat);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool IsClipboardFormatAvailable(uint format);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll")]
    private static extern bool GlobalUnlock(IntPtr hMem);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);
}
