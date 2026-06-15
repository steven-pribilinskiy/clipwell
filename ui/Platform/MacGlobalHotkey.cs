using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Clipwell.Ui.Platform;

/// <summary>
/// macOS global hotkey via Carbon's <c>RegisterEventHotKey</c>, dispatched on the
/// application event target (the main NSApplication run loop that Avalonia drives).
/// Default chord is Option+Shift+V (the macOS equivalent of Alt+Shift+V).
/// </summary>
/// <remarks>
/// Carbon is old but <c>RegisterEventHotKey</c> remains the standard, permission-free
/// way to register a process-wide hotkey (unlike an NSEvent global monitor, which
/// needs Accessibility). Implemented and compile-checked in CI; runtime behavior is
/// pending verification on real macOS hardware. All interop is guarded so a failure
/// degrades to tray-only rather than crashing.
/// </remarks>
[SupportedOSPlatform("macos")]
public sealed class MacGlobalHotkey : IGlobalHotkey
{
    private const string Carbon = "/System/Library/Frameworks/Carbon.framework/Carbon";

    // Carbon modifier masks and the 'V' virtual key code.
    private const uint ShiftKey = 0x0200, OptionKey = 0x0800;
    private const uint KVK_ANSI_V = 0x09;
    private const uint EventClassKeyboard = 0x6B657962; // 'keyb'
    private const uint EventHotKeyPressed = 5;

    public event Action? Pressed;

    // UnmanagedCallersOnly can't capture instance state, so the (single) instance is
    // referenced statically — the app registers exactly one hotkey.
    private static MacGlobalHotkey? _instance;
    private IntPtr _hotKeyRef;
    private IntPtr _handlerRef;
    private HandlerDelegate? _handler; // kept alive against GC

    public bool Register()
    {
        try
        {
            _instance = this;

            var spec = new EventTypeSpec { EventClass = EventClassKeyboard, EventKind = EventHotKeyPressed };
            _handler = OnHotKey;
            var target = GetApplicationEventTarget();
            var status = InstallEventHandler(target, Marshal.GetFunctionPointerForDelegate(_handler),
                1, new[] { spec }, IntPtr.Zero, out _handlerRef);
            if (status != 0) return false;

            var id = new EventHotKeyID { Signature = 0x636C7077 /* 'clpw' */, Id = 1 };
            status = RegisterEventHotKey(KVK_ANSI_V, OptionKey | ShiftKey, id, target, 0, out _hotKeyRef);
            return status == 0;
        }
        catch
        {
            return false;
        }
    }

    private delegate int HandlerDelegate(IntPtr callRef, IntPtr evt, IntPtr userData);

    private static int OnHotKey(IntPtr callRef, IntPtr evt, IntPtr userData)
    {
        try { _instance?.Pressed?.Invoke(); } catch { /* never throw across the native boundary */ }
        return 0; // noErr
    }

    public void Dispose()
    {
        try
        {
            if (_hotKeyRef != IntPtr.Zero) { UnregisterEventHotKey(_hotKeyRef); _hotKeyRef = IntPtr.Zero; }
            if (_handlerRef != IntPtr.Zero) { RemoveEventHandler(_handlerRef); _handlerRef = IntPtr.Zero; }
        }
        catch { /* best effort */ }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct EventTypeSpec { public uint EventClass; public uint EventKind; }

    [StructLayout(LayoutKind.Sequential)]
    private struct EventHotKeyID { public uint Signature; public uint Id; }

    [DllImport(Carbon)] private static extern IntPtr GetApplicationEventTarget();
    [DllImport(Carbon)] private static extern int InstallEventHandler(IntPtr target, IntPtr handler, int numTypes, EventTypeSpec[] typeList, IntPtr userData, out IntPtr handlerRef);
    [DllImport(Carbon)] private static extern int RemoveEventHandler(IntPtr handlerRef);
    [DllImport(Carbon)] private static extern int RegisterEventHotKey(uint hotKeyCode, uint hotKeyModifiers, EventHotKeyID hotKeyID, IntPtr target, uint options, out IntPtr hotKeyRef);
    [DllImport(Carbon)] private static extern int UnregisterEventHotKey(IntPtr hotKeyRef);
}
