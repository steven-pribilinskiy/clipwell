using System;
using Avalonia.Controls;
using Avalonia.Interactivity;

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
        var days = await _client.GetRetentionAsync();
        var idx = Array.IndexOf(Retentions, days);
        RetentionBox.SelectedIndex = idx >= 0 ? idx : 1;
    }

    private async void OnSave(object? sender, RoutedEventArgs e)
    {
        var idx = Math.Clamp(RetentionBox.SelectedIndex, 0, Retentions.Length - 1);
        try
        {
            await _client.SetRetentionAsync(Retentions[idx]);
            StatusText.Text = "Saved.";
        }
        catch
        {
            StatusText.Text = "Could not reach the daemon.";
        }
    }
}
