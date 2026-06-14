using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Threading;
using Clipwell.Ui.Platform;

namespace Clipwell.Ui;

public partial class App : Application
{
    private MainWindow? _window;
    private TrayIcon? _tray;
    private IGlobalHotkey? _hotkey;
    private IPasteService? _paste;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Background app: closing the picker window must not quit the process.
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            if (OperatingSystem.IsWindows()) _paste = new WindowsPasteService();
            _window = new MainWindow(_paste);
            SetUpTray(desktop);
            SetUpHotkey();

            // Show once on launch so the app is discoverable.
            _window.ShowPicker();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void SetUpTray(IClassicDesktopStyleApplicationLifetime desktop)
    {
        WindowIcon? icon = null;
        try
        {
            using var stream = AssetLoader.Open(new Uri("avares://Clipwell.Ui/Assets/tray.png"));
            icon = new WindowIcon(stream);
        }
        catch
        {
            // No icon asset → tray may not appear, but the hotkey still works.
        }

        var menu = new NativeMenu();
        var show = new NativeMenuItem("Show picker");
        show.Click += (_, _) => _window?.ShowPicker();
        var settings = new NativeMenuItem("Settings…");
        settings.Click += (_, _) => ShowSettings();
        var quit = new NativeMenuItem("Quit Clipwell");
        quit.Click += (_, _) => desktop.Shutdown();
        menu.Add(show);
        menu.Add(settings);
        menu.Add(new NativeMenuItemSeparator());
        menu.Add(quit);

        _tray = new TrayIcon { ToolTipText = "Clipwell", Menu = menu };
        if (icon is not null) _tray.Icon = icon;
        _tray.Clicked += (_, _) => _window?.ShowPicker();
    }

    private SettingsWindow? _settingsWindow;

    private void ShowSettings()
    {
        if (_settingsWindow is null || !_settingsWindow.IsVisible)
        {
            _settingsWindow = new SettingsWindow();
            _settingsWindow.Show();
        }
        _settingsWindow.Activate();
    }

    private void SetUpHotkey()
    {
        if (!OperatingSystem.IsWindows()) return; // mac/Linux hotkeys: a later phase
        _hotkey = new WindowsGlobalHotkey();
        _hotkey.Pressed += () =>
        {
            // Capture the source window NOW, before the picker steals focus.
            var target = _paste?.GetForegroundWindow() ?? 0;
            Dispatcher.UIThread.Post(() => _window?.ShowPicker(target));
        };
        _hotkey.Register();
    }
}
