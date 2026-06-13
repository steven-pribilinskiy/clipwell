using Clipwell.Protocol;

namespace Clipwell.Daemon;

/// <summary>
/// Watches the OS clipboard and raises <see cref="Changed"/> with a captured row
/// each time the clipboard content changes. One implementation per platform sits
/// behind this interface so the daemon pipeline is OS-agnostic.
/// </summary>
public interface IClipboardWatcher : IDisposable
{
    /// <summary>Raised on the watcher's own thread when the clipboard changes.</summary>
    event Action<StoreRow>? Changed;

    /// <summary>Begins watching. Idempotent.</summary>
    void Start();
}
