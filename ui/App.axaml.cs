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
    /// <summary>Raised by the settings window after a save, to re-apply live.</summary>
    public static event Action? SettingsChanged;
    public static void NotifySettingsChanged() => SettingsChanged?.Invoke();

    private MainWindow? _window;
    private TrayIcon? _tray;
    private IGlobalHotkey? _hotkey;
    private IPasteService? _paste;
    private IPointerLocation? _pointer;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Background app: closing the picker window must not quit the process.
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Screenshot hook: force a theme variant (otherwise we follow the OS).
            RequestedThemeVariant = Environment.GetEnvironmentVariable("CLIPWELL_THEME")?.ToLowerInvariant() switch
            {
                "light" => Avalonia.Styling.ThemeVariant.Light,
                "dark" => Avalonia.Styling.ThemeVariant.Dark,
                _ => Avalonia.Styling.ThemeVariant.Default,
            };

            if (OperatingSystem.IsWindows())
            {
                _paste = new WindowsPasteService();
                _pointer = new WindowsPointerLocation();
            }
            else if (OperatingSystem.IsLinux())
            {
                _paste = new LinuxPasteService();
            }
            else if (OperatingSystem.IsMacOS())
            {
                _paste = new MacPasteService();
            }
            _window = new MainWindow(_paste, _pointer);
            SetUpTray(desktop);
            SetUpHotkey();

            // Show once on launch so the app is discoverable.
            _window.ShowPicker();

            // Apply persisted UI prefs (theme, view/grouping, metadata, hotkey), and
            // re-apply whenever settings are saved (live, no restart).
            SettingsChanged += () => Dispatcher.UIThread.Post(() => _ = ApplySavedSettingsAsync());
            _ = ApplySavedSettingsAsync();

            // Screenshot-test hook: open Settings on launch so it can be captured
            // without driving the tray menu. Same spirit as CLIPWELL_NO_AUTOHIDE.
            if (Environment.GetEnvironmentVariable("CLIPWELL_SHOW_SETTINGS") == "1")
                Dispatcher.UIThread.Post(ShowSettings);
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

    private async System.Threading.Tasks.Task ApplySavedSettingsAsync()
    {
        try
        {
            var s = await new ClipwellClient().GetSettingsAsync();
            // Env theme (screenshot capture) wins over the saved preference.
            if (Environment.GetEnvironmentVariable("CLIPWELL_THEME") is null)
                RequestedThemeVariant = s.Theme switch
                {
                    "light" => Avalonia.Styling.ThemeVariant.Light,
                    "dark" => Avalonia.Styling.ThemeVariant.Dark,
                    _ => Avalonia.Styling.ThemeVariant.Default,
                };
            _window?.ApplyPreferences(s);
            _hotkey?.Rebind(HotkeyChord.Parse(s.Hotkey)); // live on Windows; next launch on mac/Linux
        }
        catch { /* daemon not reachable yet — defaults stand */ }
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
        _hotkey = OperatingSystem.IsWindows() ? new WindowsGlobalHotkey()
            : OperatingSystem.IsLinux() ? new LinuxGlobalHotkey()
            : OperatingSystem.IsMacOS() ? new MacGlobalHotkey()
            : null;
        if (_hotkey is null) return;

        _hotkey.Pressed += () =>
        {
            // Capture the source window NOW, before the picker steals focus (Windows;
            // a no-op on mac/Linux, where Hide() returns focus to the prior app).
            var target = _paste?.GetForegroundWindow() ?? 0;
            Dispatcher.UIThread.Post(() => _window?.ShowPicker(target));
        };
        if (!_hotkey.Register(HotkeyChord.Default)) // saved chord applied by ApplySavedSettingsAsync
            _hotkey = null; // registration failed (e.g. Wayland / no perms) → tray-only
    }
}
