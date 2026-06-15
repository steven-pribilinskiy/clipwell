using System;
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
        Opened += OnOpened;
        CloseButton.Click += (_, _) => Close();
        SaveButton.Click += OnSave;
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        var settings = await _client.GetSettingsAsync();
        var idx = Array.IndexOf(Retentions, settings.RetentionDays);
        RetentionBox.SelectedIndex = idx >= 0 ? idx : 1;
        OpenAtCursorBox.IsChecked = settings.OpenAtCursor;
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
            });
            StatusText.Text = "Saved. (Re-open the picker for position changes to apply.)";
        }
        catch
        {
            StatusText.Text = "Could not reach the daemon.";
        }
    }
}
