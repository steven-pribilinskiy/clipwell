using Clipwell.Protocol;

namespace Clipwell.Daemon;

/// <summary>
/// Placeholder watcher for platforms without a native implementation yet
/// (macOS / Linux land in a later phase). Keeps the daemon runnable everywhere:
/// the REST/WS API and stored history work; only live capture is inert.
/// </summary>
public sealed class NullClipboardWatcher : IClipboardWatcher
{
    public event Action<StoreRow>? Changed;

    public void Start()
    {
        // Reference the event so the compiler doesn't warn it is unused; no-op.
        _ = Changed;
    }

    public void Dispose() { }
}
