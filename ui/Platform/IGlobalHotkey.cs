using System;

namespace Clipwell.Ui.Platform;

/// <summary>
/// Registers a system-wide hotkey that shows the picker. One implementation per OS;
/// the rest of the app just listens to <see cref="Pressed"/>.
/// </summary>
public interface IGlobalHotkey : IDisposable
{
    /// <summary>Raised (on a background thread) when the hotkey is pressed.</summary>
    event Action? Pressed;

    /// <summary>Registers the given chord. Returns false if registration failed.</summary>
    bool Register(HotkeyChord chord);

    /// <summary>Re-register with a new chord at runtime (returns false on failure).</summary>
    bool Rebind(HotkeyChord chord);
}
