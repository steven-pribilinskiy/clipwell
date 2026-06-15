using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;

namespace Clipwell.Ui.Platform;

/// <summary>
/// macOS paste service. Hiding our window returns focus to the previous app, so we
/// synthesize Cmd+V there via CoreGraphics events. No window handle to capture.
/// </summary>
/// <remarks>
/// Posting synthetic key events needs Accessibility permission (System Settings →
/// Privacy &amp; Security → Accessibility); without it CGEventPost is a no-op and the
/// user pastes manually. Compile-checked in CI; runtime behavior pending verification
/// on real macOS hardware.
/// </remarks>
[SupportedOSPlatform("macos")]
public sealed class MacPasteService : IPasteService
{
    private const string AppServices = "/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices";

    private const ushort KVK_ANSI_V = 0x09;
    private const ulong MaskCommand = 0x100000; // kCGEventFlagMaskCommand
    private const uint HidEventTap = 0;          // kCGHIDEventTap

    public nint GetForegroundWindow() => 0;

    public void PasteInto(nint target)
    {
        Thread.Sleep(60); // let focus return to the previous app after Hide()
        try
        {
            var src = CGEventSourceCreate(1 /* kCGEventSourceStateHIDSystemState */);
            var down = CGEventCreateKeyboardEvent(src, KVK_ANSI_V, true);
            var up = CGEventCreateKeyboardEvent(src, KVK_ANSI_V, false);
            if (down == IntPtr.Zero || up == IntPtr.Zero) return;
            CGEventSetFlags(down, MaskCommand);
            CGEventSetFlags(up, MaskCommand);
            CGEventPost(HidEventTap, down);
            CGEventPost(HidEventTap, up);
            CFRelease(down); CFRelease(up);
            if (src != IntPtr.Zero) CFRelease(src);
        }
        catch
        {
            // No Accessibility permission or interop failure — item is still copied.
        }
    }

    [DllImport(AppServices)] private static extern IntPtr CGEventSourceCreate(int stateID);
    [DllImport(AppServices)] private static extern IntPtr CGEventCreateKeyboardEvent(IntPtr source, ushort virtualKey, bool keyDown);
    [DllImport(AppServices)] private static extern void CGEventSetFlags(IntPtr evt, ulong flags);
    [DllImport(AppServices)] private static extern void CGEventPost(uint tap, IntPtr evt);
    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern void CFRelease(IntPtr cf);
}
