using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Clipwell.Protocol;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clipwell.Ui;

/// <summary>
/// Backing model for the picker window: loads history from the daemon, keeps it
/// live via the WebSocket, and exposes a search-filtered view.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly ClipwellClient _client = new();
    private readonly CancellationTokenSource _cts = new();
    private List<ClipItem> _all = [];

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private string _status = "Connecting to daemon…";

    [ObservableProperty]
    private ClipRow? _selected;

    public ObservableCollection<ClipRow> Items { get; } = [];

    public async Task InitializeAsync()
    {
        await ReloadAsync();
        _ = _client.ListenAsync(
            () => Dispatcher.UIThread.Post(async void () => await ReloadAsync()),
            _cts.Token);
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private async Task ReloadAsync()
    {
        try
        {
            _all = [.. await _client.GetPageAsync(200)];
            ApplyFilter();
            Status = _all.Count == 0 ? "No clipboard history yet" : $"{_all.Count} items";
        }
        catch
        {
            Status = "Daemon unreachable — is it running?";
        }
    }

    private void ApplyFilter()
    {
        var q = SearchText.Trim();
        IEnumerable<ClipItem> filtered = string.IsNullOrEmpty(q)
            ? _all
            : _all.Where(i => i.TextContent?.Contains(q, StringComparison.OrdinalIgnoreCase) == true);

        Items.Clear();
        foreach (var item in filtered) Items.Add(new ClipRow(item));
        Selected = Items.FirstOrDefault();
    }

    public void Dispose() => _cts.Cancel();
}
