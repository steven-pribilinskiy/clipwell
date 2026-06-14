using System;

namespace Clipwell.Ui.Platform;

/// <summary>
/// Captures the window that had focus before the picker opened, and pastes into it
/// after the user picks an item (restore focus + synthesize the paste shortcut).
/// </summary>
public interface IPasteService
{
    /// <summary>The currently-focused window — call at hotkey time, before the picker shows.</summary>
    nint GetForegroundWindow();

    /// <summary>Restore focus to <paramref name="target"/> and send the paste shortcut.</summary>
    void PasteInto(nint target);
}
