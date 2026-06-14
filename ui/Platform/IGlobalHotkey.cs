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

    /// <summary>Registers the hotkey. Returns false if registration failed.</summary>
    bool Register();
}
