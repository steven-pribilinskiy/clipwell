namespace Clipwell.Protocol.Plugins;

/// <summary>
/// Services an <see cref="IClipAction"/> can use to do its work without depending on
/// the UI directly. The host (picker) provides the implementation, so the same action
/// works whether it ships in the core or loads from a plugin.
/// </summary>
public interface IClipActionContext
{
    /// <summary>Open a URL in the default browser.</summary>
    void OpenUrl(string url);

    /// <summary>Open a file or folder with its default handler.</summary>
    void OpenPath(string path);

    /// <summary>Put text on the system clipboard.</summary>
    Task SetClipboardAsync(string text);

    /// <summary>Surface a short status message to the user.</summary>
    void Notify(string message);
}
