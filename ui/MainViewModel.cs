using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Clipwell.Protocol;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Clipwell.Ui;

public enum ClipFilter
{
    All,
    Pinned,
    Sensitive,
}

/// <summary>
/// Backing model for the picker window: loads history from the daemon, keeps it
/// live via the WebSocket, and exposes a search- and tab-filtered view.
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

    [ObservableProperty]
    private ClipFilter _filter = ClipFilter.All;

    public ObservableCollection<ClipRow> Items { get; } = [];

    public ClipwellClient Client => _client;

    public async Task InitializeAsync()
    {
        await ReloadAsync();
        _ = _client.ListenAsync(
            () => Dispatcher.UIThread.Post(async void () => await ReloadAsync()),
            _cts.Token);
    }

    /// <summary>Force a fresh load (used when the picker is re-shown).</summary>
    public Task RefreshAsync() => ReloadAsync();

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnFilterChanged(ClipFilter value) => ApplyFilter();

    [RelayCommand]
    private void SetFilter(string filter) =>
        Filter = Enum.TryParse<ClipFilter>(filter, out var f) ? f : ClipFilter.All;

    [RelayCommand]
    private async Task TogglePinAsync()
    {
        if (Selected is null) return;
        await _client.PinAsync(Selected.Item.Timestamp, !Selected.Item.IsUserPinned);
        await ReloadAsync();
    }

    [RelayCommand]
    private async Task ToggleSensitiveAsync()
    {
        if (Selected is null) return;
        await _client.SensitiveAsync(Selected.Item.Timestamp, !Selected.Item.IsSensitive);
        await ReloadAsync();
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        if (Selected is null) return;
        await _client.DeleteAsync(Selected.Item.Timestamp);
        await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        try
        {
            _all = [.. await _client.GetPageAsync(200)];
            ApplyFilter();
        }
        catch
        {
            Status = "Daemon unreachable — is it running?";
        }
    }

    private void ApplyFilter()
    {
        var q = SearchText.Trim();
        IEnumerable<ClipItem> filtered = _all;

        filtered = Filter switch
        {
            ClipFilter.Pinned => filtered.Where(i => i.IsUserPinned),
            ClipFilter.Sensitive => filtered.Where(i => i.IsSensitive),
            _ => filtered,
        };

        if (!string.IsNullOrEmpty(q))
            filtered = filtered.Where(i =>
                i.TextContent?.Contains(q, StringComparison.OrdinalIgnoreCase) == true ||
                i.Alias?.Contains(q, StringComparison.OrdinalIgnoreCase) == true);

        // Pinned items float to the top, otherwise newest-first order is preserved.
        var ordered = filtered.OrderByDescending(i => i.IsUserPinned);

        Items.Clear();
        foreach (var item in ordered) Items.Add(new ClipRow(item, _client));
        Selected = Items.FirstOrDefault();

        // Reflect the filtered view, not just the last reload (search filters live).
        Status = _all.Count == 0
            ? "No clipboard history yet"
            : $"{Items.Count} of {_all.Count} items";
    }

    public void Dispose() => _cts.Cancel();
}
