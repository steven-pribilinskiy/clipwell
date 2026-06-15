using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Clipwell.Protocol.Plugins;

namespace Clipwell.Ui.Actions;

/// <summary>
/// Host implementation of <see cref="IClipActionContext"/> for the picker: opens
/// URLs/paths via the shell and writes the clipboard via the window's clipboard.
/// </summary>
public sealed class ActionContext(IClipboard? clipboard, Action<string>? notify = null) : IClipActionContext
{
    public void OpenUrl(string url) => Shell(url);

    public void OpenPath(string path) => Shell(path);

    public async Task SetClipboardAsync(string text)
    {
        if (clipboard is not null) await clipboard.SetTextAsync(text);
    }

    public void Notify(string message) => notify?.Invoke(message);

    private static void Shell(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = target, UseShellExecute = true });
        }
        catch
        {
            // bad URL/path / no handler — ignore
        }
    }
}
