using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
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

/// <summary>A grouping option for the picker (none / date / source).</summary>
public sealed record GroupOption(string Label, string Value)
{
    public override string ToString() => Label;
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

    public IReadOnlyList<GroupOption> GroupOptions { get; } =
    [
        new("No grouping", "none"),
        new("By date", "date"),
        new("By source", "source"),
    ];

    [ObservableProperty]
    private GroupOption _selectedGroup;

    [ObservableProperty]
    private bool _isRenaming;

    [ObservableProperty]
    private string _renameText = "";

    // ── Detail view (split list + preview pane) ──────────────────────────
    [ObservableProperty]
    private bool _isDetail;

    [ObservableProperty]
    private Bitmap? _previewImage;

    public string ViewToggleLabel => IsDetail ? "▤" : "▦";
    public bool PreviewHasImage => PreviewImage is not null;
    public bool PreviewHasText => PreviewImage is null && !string.IsNullOrEmpty(Selected?.FullText);

    [RelayCommand]
    private void ToggleView() => IsDetail = !IsDetail;

    // ── Quick Look (Ctrl+Y) — fullscreen preview overlay ─────────────────
    [ObservableProperty]
    private bool _isQuickLook;

    public void ToggleQuickLook()
    {
        if (Selected is not null) IsQuickLook = !IsQuickLook;
    }

    public void CloseQuickLook() => IsQuickLook = false;

    partial void OnIsDetailChanged(bool value) => OnPropertyChanged(nameof(ViewToggleLabel));

    partial void OnPreviewImageChanged(Bitmap? value)
    {
        OnPropertyChanged(nameof(PreviewHasImage));
        OnPropertyChanged(nameof(PreviewHasText));
    }

    partial void OnSelectedChanged(ClipRow? value)
    {
        PreviewImage = null;
        OnPropertyChanged(nameof(PreviewHasText));
        if (value?.Item is { HasImage: true, IsSensitive: false } item)
            _ = LoadPreviewImageAsync(item.Timestamp);
    }

    private async Task LoadPreviewImageAsync(string timestamp)
    {
        var bytes = await _client.GetImageBytesAsync(timestamp);
        if (bytes is null) return;
        try
        {
            using var ms = new System.IO.MemoryStream(bytes);
            var bmp = new Bitmap(ms); // full-res for the preview pane
            // Ignore if the selection moved on while we were loading.
            if (Selected?.Item.Timestamp != timestamp) return;
            PreviewImage = bmp;
        }
        catch { /* undecodable */ }
    }

    public ObservableCollection<ClipRow> Items { get; } = [];

    public ClipwellClient Client => _client;

    public MainViewModel()
    {
        _selectedKind = KindOptions[0];   // "All types"
        _selectedGroup = GroupOptions[0]; // "No grouping"
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

    /// <summary>Apply persisted UI preferences (default view + grouping) and refresh.</summary>
    public void ApplyPreferences(ClipboardSettings s)
    {
        IsDetail = s.DefaultView == "detail";
        var g = GroupOptions.FirstOrDefault(o => o.Value == s.DefaultGroup);
        if (g is not null) SelectedGroup = g;
        ApplyFilter(); // rebuild rows so metadata toggles take effect
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnFilterChanged(ClipFilter value) => ApplyFilter();
    partial void OnSelectedKindChanged(KindOption value) => ApplyFilter();
    partial void OnSelectedGroupChanged(GroupOption value) => ApplyFilter();

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

            var group = SelectedGroup?.Value ?? "none";
            foreach (var item in fresh.Where(PassesFilter))
            {
                var row = new ClipRow(item, _client);
                AssignHeader(row, group);
                Items.Add(row);
            }
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

    private string? _lastBucket;

    private void ApplyFilter()
    {
        var group = SelectedGroup?.Value ?? "none";
        var filtered = _all.Where(PassesFilter);

        // Date grouping keeps the store's newest-first order; source grouping sorts by
        // app then time; otherwise pinned items float to the top.
        IEnumerable<ClipItem> ordered = group switch
        {
            "source" => filtered.OrderBy(SourceBucket, StringComparer.OrdinalIgnoreCase)
                                .ThenByDescending(i => i.Timestamp, StringComparer.Ordinal),
            "date" => filtered,
            _ => filtered.OrderByDescending(i => i.IsUserPinned),
        };

        Items.Clear();
        _lastBucket = null;
        foreach (var item in ordered)
        {
            var row = new ClipRow(item, _client);
            AssignHeader(row, group);
            Items.Add(row);
        }
        Selected = Items.FirstOrDefault();

        // Reflect the filtered view, not just the last reload (search filters live).
        Status = _all.Count == 0
            ? "No clipboard history yet"
            : $"{Items.Count} of {_all.Count} items";
    }

    private void AssignHeader(ClipRow row, string group)
    {
        if (group == "none") return;
        var bucket = group == "source" ? SourceBucket(row.Item) : DateBucket(row.Item.Timestamp);
        if (bucket != _lastBucket)
        {
            row.GroupHeader = bucket;
            _lastBucket = bucket;
        }
    }

    private static string SourceBucket(ClipItem i) =>
        string.IsNullOrEmpty(i.SourceApp) ? "Unknown source" : i.SourceApp;

    private static string DateBucket(string timestamp)
    {
        if (!DateTimeOffset.TryParse(timestamp, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal, out var ts))
            return "Earlier";
        var day = ts.LocalDateTime.Date;
        var today = DateTime.Now.Date;
        if (day == today) return "Today";
        if (day == today.AddDays(-1)) return "Yesterday";
        var delta = DateTime.Now - ts.LocalDateTime;
        if (delta < TimeSpan.FromDays(7)) return "Previous 7 days";
        if (delta < TimeSpan.FromDays(30)) return "Previous 30 days";
        return "Earlier";
    }

    public void Dispose() => _cts.Cancel();
}
