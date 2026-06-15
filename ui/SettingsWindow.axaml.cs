using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Clipwell.Protocol;

namespace Clipwell.Ui;

public partial class SettingsWindow : Window
{
    private readonly ClipwellClient _client = new();

    // ComboBox index ↔ retention days (null = forever).
    private static readonly int?[] Retentions = [7, 30, 90, null];

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
    }

    private static readonly string[] Themes = ["system", "light", "dark"];
    private static readonly string[] Views = ["compact", "detail"];
    private static readonly string[] Groups = ["none", "date", "source"];

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
            });
            StatusText.Text = "Saved. Re-open the picker to apply.";
        }
        catch
        {
            StatusText.Text = "Could not reach the daemon.";
        }
    }
}
