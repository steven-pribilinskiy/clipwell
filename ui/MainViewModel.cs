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

/// <summary>A selectable kind filter for the picker's type dropdown.</summary>
public sealed record KindOption(string Label, string Value)
{
    public override string ToString() => Label; // ComboBox display
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

    /// <summary>Type-filter options for the dropdown (label → kind id; "all" = no filter).</summary>
    public IReadOnlyList<KindOption> KindOptions { get; } =
    [
        new("All types", "all"),
        new("Text", "text"),
        new("Link", "url"),
        new("GitHub PR", "github-pr"),
        new("Jira issue", "jira-issue"),
        new("Email", "email"),
        new("Color", "color"),
        new("Path", "path"),
        new("Code", "code"),
        new("Image", "image"),
    ];

    [ObservableProperty]
    private KindOption _selectedKind;

    [ObservableProperty]
    private bool _isRenaming;

    [ObservableProperty]
    private string _renameText = "";

    public ObservableCollection<ClipRow> Items { get; } = [];

    public ClipwellClient Client => _client;

    public MainViewModel()
    {
        _selectedKind = KindOptions[0]; // "All types"
    }

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
    partial void OnSelectedKindChanged(KindOption value) => ApplyFilter();

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

    /// <summary>Open the rename bar for the selected item, pre-filled with its alias.</summary>
    public void BeginRename()
    {
        if (Selected is null) return;
        RenameText = Selected.Item.Alias ?? "";
        IsRenaming = true;
    }

    public void CancelRename() => IsRenaming = false;

    [RelayCommand]
    private async Task CommitRenameAsync()
    {
        if (Selected is not null)
        {
            var alias = string.IsNullOrWhiteSpace(RenameText) ? null : RenameText.Trim();
            await _client.RenameAsync(Selected.Item.Timestamp, alias);
            await ReloadAsync();
        }
        IsRenaming = false;
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        if (Selected is null) return;
        await _client.DeleteAsync(Selected.Item.Timestamp);
        await ReloadAsync();
    }

    private const int PageSize = 100;
    private string? _oldestLoaded;
    private bool _allLoaded;
    private bool _loadingMore;

    private async Task ReloadAsync()
    {
        try
        {
            _all = [.. await _client.GetPageAsync(PageSize)];
            _oldestLoaded = _all.Count > 0 ? _all[^1].Timestamp : null;
            _allLoaded = _all.Count < PageSize;
            ApplyFilter();
        }
        catch
        {
            Status = "Daemon unreachable — is it running?";
        }
    }

    /// <summary>
    /// Fetches the next older page and appends matching rows in place (preserving
    /// scroll position). Called by the picker when the list nears its bottom.
    /// </summary>
    public async Task LoadMoreAsync()
    {
        if (_loadingMore || _allLoaded || _oldestLoaded is null) return;
        _loadingMore = true;
        try
        {
            var page = await _client.GetPageAsync(PageSize, _oldestLoaded);
            if (page.Count == 0) { _allLoaded = true; return; }

            var seen = _all.Select(i => i.Timestamp).ToHashSet();
            var fresh = page.Where(i => seen.Add(i.Timestamp)).ToList();
            _all.AddRange(fresh);
            _oldestLoaded = _all[^1].Timestamp;
            if (page.Count < PageSize) _allLoaded = true;

            foreach (var item in fresh.Where(PassesFilter))
                Items.Add(new ClipRow(item, _client));
            Status = $"{Items.Count} of {_all.Count} items";
        }
        catch
        {
            // network blip — try again on the next scroll
        }
        finally
        {
            _loadingMore = false;
        }
    }

    private bool PassesFilter(ClipItem i)
    {
        if (Filter == ClipFilter.Pinned && !i.IsUserPinned) return false;
        if (Filter == ClipFilter.Sensitive && !i.IsSensitive) return false;
        var kind = SelectedKind?.Value ?? "all";
        if (kind != "all" && i.Kind != kind) return false;
        var q = SearchText.Trim();
        if (!string.IsNullOrEmpty(q) &&
            !(i.TextContent?.Contains(q, StringComparison.OrdinalIgnoreCase) == true ||
              i.Alias?.Contains(q, StringComparison.OrdinalIgnoreCase) == true))
            return false;
        return true;
    }

    private void ApplyFilter()
    {
        // Pinned items float to the top, otherwise newest-first order is preserved.
        var ordered = _all.Where(PassesFilter).OrderByDescending(i => i.IsUserPinned);

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
