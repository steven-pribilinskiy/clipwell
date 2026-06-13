using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;

namespace Clipwell.Ui;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;
        Opened += OnOpened;
        Closed += (_, _) => _vm.Dispose();
        ItemsList.DoubleTapped += (_, _) => _ = CopySelectedAndCloseAsync();
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        SearchBox.Focus();
        await _vm.InitializeAsync();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                Close();
                e.Handled = true;
                break;
            case Key.Enter:
                _ = CopySelectedAndCloseAsync();
                e.Handled = true;
                break;
            case Key.Down when SearchBox.IsFocused && ItemsList.ItemCount > 0:
                ItemsList.Focus();
                e.Handled = true;
                break;
        }
        base.OnKeyDown(e);
    }

    private async Task CopySelectedAndCloseAsync()
    {
        var text = _vm.Selected?.Item.TextContent;
        if (!string.IsNullOrEmpty(text) && Clipboard is not null)
            await Clipboard.SetTextAsync(text);
        Close();
    }
}
