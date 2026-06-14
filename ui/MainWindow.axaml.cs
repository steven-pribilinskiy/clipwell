using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Clipwell.Ui.Platform;

namespace Clipwell.Ui;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();
    private readonly IPasteService? _paste;
    private bool _initialized;
    private nint _pasteTarget;

    // Hide when focus is lost (normal picker behavior). Disabled for automated
    // tests via CLIPWELL_NO_AUTOHIDE so a screenshot can be captured.
    private static readonly bool AutoHide =
        Environment.GetEnvironmentVariable("CLIPWELL_NO_AUTOHIDE") is null;

    public MainWindow() : this(null) { }

    public MainWindow(IPasteService? paste)
    {
        _paste = paste;
        InitializeComponent();
        DataContext = _vm;
        Closed += (_, _) => _vm.Dispose();
        ItemsList.DoubleTapped += (_, _) => _ = CopySelectedAndHideAsync();
        Deactivated += (_, _) => { if (AutoHide && IsVisible) Hide(); };
    }

    /// <summary>
    /// Shows the pre-warmed window: resets search, refreshes, focuses, and records
    /// the show-cycle latency. Called at startup and on the global hotkey.
    /// </summary>
    public void ShowPicker(nint pasteTarget = 0)
    {
        _pasteTarget = pasteTarget;
        var sw = Stopwatch.StartNew();
        _vm.SearchText = "";
        Show();
        Activate();
        SearchBox.Focus();
        sw.Stop();
        PerfLog.RecordShow(sw.Elapsed.TotalMilliseconds);

        if (!_initialized)
        {
            _initialized = true;
            _ = _vm.InitializeAsync();
        }
        else
        {
            _ = _vm.RefreshAsync();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                Hide();
                e.Handled = true;
                break;
            case Key.Enter:
                _ = CopySelectedAndHideAsync();
                e.Handled = true;
                break;
            case Key.Down when SearchBox.IsFocused && ItemsList.ItemCount > 0:
                ItemsList.Focus();
                e.Handled = true;
                break;
            case Key.Delete:
                _vm.DeleteSelectedCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.P when e.KeyModifiers.HasFlag(KeyModifiers.Control):
                _vm.TogglePinCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.E when e.KeyModifiers.HasFlag(KeyModifiers.Control):
                _vm.ToggleSensitiveCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.D1 when e.KeyModifiers.HasFlag(KeyModifiers.Control):
                _vm.SetFilterCommand.Execute("All");
                e.Handled = true;
                break;
            case Key.D2 when e.KeyModifiers.HasFlag(KeyModifiers.Control):
                _vm.SetFilterCommand.Execute("Pinned");
                e.Handled = true;
                break;
            case Key.D3 when e.KeyModifiers.HasFlag(KeyModifiers.Control):
                _vm.SetFilterCommand.Execute("Sensitive");
                e.Handled = true;
                break;
        }
        base.OnKeyDown(e);
    }

    private async Task CopySelectedAndHideAsync()
    {
        var text = _vm.Selected?.Item.TextContent;
        if (string.IsNullOrEmpty(text))
        {
            Hide();
            return;
        }
        if (Clipboard is not null)
            await Clipboard.SetTextAsync(text);
        var target = _pasteTarget;
        Hide();
        // Restore focus to the source app and paste the selection there.
        if (target != 0) _paste?.PasteInto(target);
    }
}
