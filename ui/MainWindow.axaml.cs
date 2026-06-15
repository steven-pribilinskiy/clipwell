using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Clipwell.Ui.Platform;

namespace Clipwell.Ui;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();
    private readonly IPasteService? _paste;
    private readonly IPointerLocation? _pointer;
    private bool _initialized;
    private bool _openAtCursor;
    private nint _pasteTarget;

    // Hide when focus is lost (normal picker behavior). Disabled for automated
    // tests via CLIPWELL_NO_AUTOHIDE so a screenshot can be captured.
    private static readonly bool AutoHide =
        Environment.GetEnvironmentVariable("CLIPWELL_NO_AUTOHIDE") is null;

    public MainWindow() : this(null) { }

    public MainWindow(IPasteService? paste, IPointerLocation? pointer = null)
    {
        _paste = paste;
        _pointer = pointer;
        InitializeComponent();
        DataContext = _vm;
        Closed += (_, _) => _vm.Dispose();
        ItemsList.DoubleTapped += (_, _) => _ = CopySelectedAndHideAsync();
        Deactivated += (_, _) => { if (AutoHide && IsVisible) Hide(); };
    }

    /// <summary>Refresh the cached open-at-cursor preference from the daemon.</summary>
    private async Task LoadPrefsAsync()
    {
        try { _openAtCursor = (await _vm.Client.GetSettingsAsync()).OpenAtCursor; }
        catch { /* keep last known / default */ }
    }

    // Position the picker before showing: at the cursor (clamped to its screen) when
    // enabled, otherwise leave it centered (the XAML default). Cheap — runs in the
    // show-cycle, so it must stay off the critical path's hot loop.
    private void PositionForShow()
    {
        if (!_openAtCursor || _pointer is null || !_pointer.TryGetCursor(out var cur))
            return;

        var screen = Screens.ScreenFromPoint(cur) ?? Screens.Primary;
        if (screen is null) return;

        var scale = screen.Scaling;
        var wa = screen.WorkingArea;
        var winW = (int)(Width * scale);
        var winH = (int)(Height * scale);
        // Put the cursor just inside the search box, then clamp so the window stays
        // fully on the working area.
        var x = Math.Clamp(cur.X - (int)(40 * scale), wa.X, Math.Max(wa.X, wa.Right - winW));
        var y = Math.Clamp(cur.Y - (int)(20 * scale), wa.Y, Math.Max(wa.Y, wa.Bottom - winH));
        WindowStartupLocation = WindowStartupLocation.Manual;
        Position = new PixelPoint(x, y);
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
        PositionForShow();
        Show();
        Activate();
        SearchBox.Focus();
        sw.Stop();
        PerfLog.RecordShow(sw.Elapsed.TotalMilliseconds);

        if (!_initialized)
        {
            _initialized = true;
            _ = _vm.InitializeAsync();
            _ = LoadPrefsAsync();
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
