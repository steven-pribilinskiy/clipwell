using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Clipwell.Protocol;
using Clipwell.Ui.Platform;

namespace Clipwell.Ui;

public partial class SettingsWindow : Window
{
    private readonly ClipwellClient _client = new();

    // ComboBox index ↔ retention days (null = forever).
    private static readonly int?[] Retentions = [7, 30, 90, null];
    private static readonly string[] Themes = ["system", "light", "dark"];
    private static readonly string[] Views = ["compact", "detail"];
    private static readonly string[] Groups = ["none", "date", "source"];

    private string _hotkey = "Alt+Shift+V";
    private bool _recording;

    public SettingsWindow()
    {
        InitializeComponent();
        // Screenshot mode: off-screen + non-activating so capture never steals focus.
        if (Environment.GetEnvironmentVariable("CLIPWELL_CAPTURE") == "1")
        {
            ShowActivated = false;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Position = new PixelPoint(-4000, -4000);
        }
        Opened += OnOpened;
        CloseButton.Click += (_, _) => Close();
        SaveButton.Click += OnSave;
        RecordButton.Click += (_, _) =>
        {
            _recording = true;
            HotkeyText.Text = "Press a combo…";
        };
        AddHandler(KeyDownEvent, OnRecordKey, Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }

    private void OnRecordKey(object? sender, KeyEventArgs e)
    {
        if (!_recording) return;
        var key = KeyToString(e.Key);
        if (key is null) return; // modifier-only or unsupported — keep waiting
        var chord = new HotkeyChord(
            Alt: e.KeyModifiers.HasFlag(KeyModifiers.Alt),
            Ctrl: e.KeyModifiers.HasFlag(KeyModifiers.Control),
            Shift: e.KeyModifiers.HasFlag(KeyModifiers.Shift),
            Win: e.KeyModifiers.HasFlag(KeyModifiers.Meta),
            Key: key);
        if (!chord.Alt && !chord.Ctrl && !chord.Shift && !chord.Win) return; // need a modifier
        _hotkey = chord.Display;
        HotkeyText.Text = _hotkey;
        _recording = false;
        e.Handled = true;
    }

    private static string? KeyToString(Key k)
    {
        if (k is >= Key.A and <= Key.Z) return k.ToString();
        if (k is >= Key.D0 and <= Key.D9) return ((char)('0' + (k - Key.D0))).ToString();
        if (k is >= Key.NumPad0 and <= Key.NumPad9) return ((char)('0' + (k - Key.NumPad0))).ToString();
        if (k is >= Key.F1 and <= Key.F12) return k.ToString();
        return null;
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        var s = await _client.GetSettingsAsync();
        var idx = Array.IndexOf(Retentions, s.RetentionDays);
        RetentionBox.SelectedIndex = idx >= 0 ? idx : 1;
        OpenAtCursorBox.IsChecked = s.OpenAtCursor;
        ThemeBox.SelectedIndex = Math.Max(0, Array.IndexOf(Themes, s.Theme));
        ViewBox.SelectedIndex = Math.Max(0, Array.IndexOf(Views, s.DefaultView));
        GroupBox.SelectedIndex = Math.Max(0, Array.IndexOf(Groups, s.DefaultGroup));
        ShowSourceBox.IsChecked = s.ShowSource;
        ShowTimeBox.IsChecked = s.ShowTime;
        _hotkey = string.IsNullOrWhiteSpace(s.Hotkey) ? "Alt+Shift+V" : s.Hotkey;
        HotkeyText.Text = _hotkey;
    }

    private async void OnSave(object? sender, RoutedEventArgs e)
    {
        var idx = Math.Clamp(RetentionBox.SelectedIndex, 0, Retentions.Length - 1);
        try
        {
            await _client.SaveSettingsAsync(new ClipboardSettings
            {
                RetentionDays = Retentions[idx],
                OpenAtCursor = OpenAtCursorBox.IsChecked == true,
                Theme = Themes[Math.Max(0, ThemeBox.SelectedIndex)],
                DefaultView = Views[Math.Max(0, ViewBox.SelectedIndex)],
                DefaultGroup = Groups[Math.Max(0, GroupBox.SelectedIndex)],
                ShowSource = ShowSourceBox.IsChecked == true,
                ShowTime = ShowTimeBox.IsChecked == true,
                Hotkey = _hotkey,
            });
            App.NotifySettingsChanged(); // apply live (theme, view, metadata, hotkey)
            StatusText.Text = "Saved.";
        }
        catch
        {
            StatusText.Text = "Could not reach the daemon.";
        }
    }
}
