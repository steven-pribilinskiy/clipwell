using System.Text.Json;

namespace Clipwell.Daemon;

/// <summary>
/// User metadata that isn't part of the captured clipboard content — pins,
/// sensitive flags, and aliases — keyed by an item's timestamp. Persisted as JSON
/// next to the history DB so it survives restarts. Ported in spirit from the
/// original backend's <c>clipboard-meta.json</c>.
/// </summary>
public sealed class MetadataStore
{
    private sealed class Model
    {
        public HashSet<string> Pinned { get; set; } = [];
        public HashSet<string> Sensitive { get; set; } = [];
        public Dictionary<string, string> Aliases { get; set; } = [];
    }

    private readonly string _path;
    private readonly Lock _gate = new();
    private Model _model;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public MetadataStore()
    {
        var dir = Environment.GetEnvironmentVariable("CLIPWELL_DATA_DIR")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Clipwell");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "clipboard-meta.json");
        _model = Load();
    }

    private Model Load()
    {
        try
        {
            return JsonSerializer.Deserialize<Model>(File.ReadAllText(_path), JsonOpts) ?? new Model();
        }
        catch
        {
            return new Model();
        }
    }

    private void Save()
    {
        try { File.WriteAllText(_path, JsonSerializer.Serialize(_model, JsonOpts)); }
        catch { /* metadata persistence is best-effort */ }
    }

    public bool IsPinned(string ts) { lock (_gate) return _model.Pinned.Contains(ts); }
    public bool IsSensitive(string ts) { lock (_gate) return _model.Sensitive.Contains(ts); }
    public string? Alias(string ts) { lock (_gate) return _model.Aliases.GetValueOrDefault(ts); }

    public void SetPinned(string ts, bool on)
    {
        lock (_gate)
        {
            if ((on ? _model.Pinned.Add(ts) : _model.Pinned.Remove(ts))) Save();
        }
    }

    public void SetSensitive(string ts, bool on)
    {
        lock (_gate)
        {
            if ((on ? _model.Sensitive.Add(ts) : _model.Sensitive.Remove(ts))) Save();
        }
    }

    public void SetAlias(string ts, string? alias)
    {
        lock (_gate)
        {
            if (string.IsNullOrWhiteSpace(alias)) _model.Aliases.Remove(ts);
            else _model.Aliases[ts] = alias.Trim();
            Save();
        }
    }

    /// <summary>Drop all metadata for a timestamp (called when an item is deleted).</summary>
    public void Forget(string ts)
    {
        lock (_gate)
        {
            var changed = _model.Pinned.Remove(ts) | _model.Sensitive.Remove(ts) | _model.Aliases.Remove(ts);
            if (changed) Save();
        }
    }
}
