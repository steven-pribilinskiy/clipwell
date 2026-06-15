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
    private const uint CF_DIB = 8;
    private static readonly IntPtr HWND_MESSAGE = new(-3);

    private readonly string _cacheDir;
    private readonly uint _cfHtml; // registered "HTML Format"

    public WindowsClipboardWatcher(string cacheDir)
    {
        _cacheDir = cacheDir;
        _cfHtml = RegisterClipboardFormat("HTML Format");
    }

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
        if (string.IsNullOrEmpty(text)) text = null; // an empty CF_UNICODETEXT is not a real item
        var html = ReadHtml();
        var now = DateTimeOffset.UtcNow;
        var imagePath = ReadAndSaveImage(now.ToUnixTimeMilliseconds());

        // Nothing useful on the clipboard (e.g. a file drop or a format we don't read yet).
        if (text is null && html is null && imagePath is null) return;

        var formats = new List<string>();
        if (text is not null) formats.Add("text");
        if (html is not null) formats.Add("html");
        if (imagePath is not null) formats.Add("image");

        Changed?.Invoke(new StoreRow
        {
            Timestamp = now.ToString("o"),
            TextContent = text,
            TextLength = text?.Length ?? 0,
            HtmlContent = html,
            HasImage = imagePath is not null,
            ImagePath = imagePath,
            SourceApp = ReadForegroundApp(),
            Formats = formats,
        });
    }

    // Best-effort friendly name of the app that was foreground when the clipboard
    // changed — i.e. the app the user copied from. Returns null if it can't be
    // resolved (or if it would attribute to Clipwell itself).
    private static string? ReadForegroundApp()
    {
        try
        {
            var hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return null;
            _ = GetWindowThreadProcessId(hwnd, out var pid);
            if (pid == 0) return null;

            const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
            var h = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (h == IntPtr.Zero) return null;
            try
            {
                var sb = new System.Text.StringBuilder(1024);
                var cap = sb.Capacity;
                if (!QueryFullProcessImageName(h, 0, sb, ref cap)) return null;
                var path = sb.ToString();
                if (string.IsNullOrEmpty(path)) return null;

                var name = Path.GetFileNameWithoutExtension(path);
                try
                {
                    var desc = System.Diagnostics.FileVersionInfo.GetVersionInfo(path).FileDescription;
                    if (!string.IsNullOrWhiteSpace(desc)) name = desc.Trim();
                }
                catch { /* no version info — fall back to the exe name */ }

                if (name.Contains("Clipwell", StringComparison.OrdinalIgnoreCase)) return null;
                return name;
            }
            finally { CloseHandle(h); }
        }
        catch
        {
            return null; // never let source resolution break a capture
        }
    }

    private string? ReadHtml()
    {
        if (_cfHtml == 0 || !IsClipboardFormatAvailable(_cfHtml)) return null;
        if (!OpenClipboard(IntPtr.Zero)) return null;
        try
        {
            var handle = GetClipboardData(_cfHtml);
            if (handle == IntPtr.Zero) return null;
            var ptr = GlobalLock(handle);
            if (ptr == IntPtr.Zero) return null;
            try
            {
                // CF_HTML is UTF-8 bytes with a descriptor header; store as-is.
                var size = (int)(ulong)GlobalSize(handle);
                if (size <= 0) return null;
                var bytes = new byte[size];
                Marshal.Copy(ptr, bytes, 0, size);
                var s = System.Text.Encoding.UTF8.GetString(bytes).TrimEnd('\0');
                return string.IsNullOrWhiteSpace(s) ? null : s;
            }
            finally { GlobalUnlock(handle); }
        }
        finally { CloseClipboard(); }
    }

    private string? ReadAndSaveImage(long stampMs)
    {
        if (!IsClipboardFormatAvailable(CF_DIB)) return null;
        if (!OpenClipboard(IntPtr.Zero)) return null;
        try
        {
            var handle = GetClipboardData(CF_DIB);
            if (handle == IntPtr.Zero) return null;
            var ptr = GlobalLock(handle);
            if (ptr == IntPtr.Zero) return null;
            try
            {
                var size = (int)(ulong)GlobalSize(handle);
                if (size <= 0) return null;
                var dib = new byte[size];
                Marshal.Copy(ptr, dib, 0, size);
                return SaveDibAsPng(dib, stampMs);
            }
            finally { GlobalUnlock(handle); }
        }
        catch
        {
            return null; // never let an image-decode failure break capture
        }
        finally { CloseClipboard(); }
    }

    // A CF_DIB is a BMP without the 14-byte BITMAPFILEHEADER. Prepend one so
    // System.Drawing can decode it, then re-encode as PNG into the cache dir.
    private string? SaveDibAsPng(byte[] dib, long stampMs)
    {
        const int fileHeaderSize = 14;
        // bfOffBits = fileHeader + DIB header + color table. Read biSize +
        // biClrUsed/biBitCount from the BITMAPINFOHEADER to compute the table size.
        var headerSize = BitConverter.ToInt32(dib, 0);
        var bitCount = BitConverter.ToInt16(dib, 14);
        var clrUsed = BitConverter.ToInt32(dib, 32);
        var paletteEntries = clrUsed != 0 ? clrUsed : (bitCount <= 8 ? 1 << bitCount : 0);
        var offBits = fileHeaderSize + headerSize + paletteEntries * 4;

        var bmp = new byte[fileHeaderSize + dib.Length];
        bmp[0] = (byte)'B';
        bmp[1] = (byte)'M';
        BitConverter.GetBytes(bmp.Length).CopyTo(bmp, 2);
        BitConverter.GetBytes(offBits).CopyTo(bmp, 10);
        dib.CopyTo(bmp, fileHeaderSize);

        using var ms = new MemoryStream(bmp);
        using var image = System.Drawing.Image.FromStream(ms);
        var path = Path.Combine(_cacheDir, $"img-{stampMs}.png");
        image.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        return path;
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

    [DllImport("kernel32.dll")]
    private static extern UIntPtr GlobalSize(IntPtr hMem);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterClipboardFormat(string lpszFormat);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, bool inheritHandle, uint processId);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool QueryFullProcessImageName(IntPtr hProcess, uint flags, System.Text.StringBuilder exeName, ref int size);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);
}
