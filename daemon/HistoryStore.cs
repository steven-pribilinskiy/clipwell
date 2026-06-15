using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Clipwell.Daemon.Detectors;
using Clipwell.Protocol;
using Microsoft.Data.Sqlite;

namespace Clipwell.Daemon;

/// <summary>
/// SQLite-backed clipboard history. Ported from the original windows-settings
/// backend (<c>clipboard-store.ts</c>) so the on-disk schema and dedup semantics
/// are unchanged and existing <c>history.db</c> files keep working.
/// </summary>
public sealed class HistoryStore : IDisposable
{
    private readonly string _dbPath;
    private readonly string _settingsPath;
    private readonly SqliteConnection _conn;
    private readonly Lock _gate = new();
    // Built-in detectors + any loaded from plugins (CLIPWELL_PLUGINS_DIR).
    private readonly DetectorRegistry _detectors =
        new(Clipwell.Protocol.Plugins.PluginLoader.Load<Clipwell.Protocol.Plugins.IClipDetector>());
    private readonly MetadataStore _meta;

    public HistoryStore(MetadataStore meta)
    {
        _meta = meta;
        // Default: %APPDATA%\Roaming\Clipwell on Windows; ~/.config/Clipwell on
        // Linux; ~/Library/Application Support/Clipwell on macOS. Overridable via
        // CLIPWELL_DATA_DIR so dev/test runs use an isolated DB instead of the
        // user's real history.
        var storeDir = Environment.GetEnvironmentVariable("CLIPWELL_DATA_DIR")
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Clipwell");
        Directory.CreateDirectory(storeDir);
        _dbPath = Path.Combine(storeDir, "history.db");
        _settingsPath = Path.Combine(storeDir, "clipboard-settings.json");
        CacheDir = Path.Combine(storeDir, "cache");
        Directory.CreateDirectory(CacheDir);

        _conn = new SqliteConnection($"Data Source={_dbPath}");
        _conn.Open();
        Exec("PRAGMA journal_mode = WAL;");
        Exec("PRAGMA synchronous = NORMAL;");
        Exec("""
            CREATE TABLE IF NOT EXISTS items (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                timestamp TEXT NOT NULL,
                text_content TEXT,
                text_length INTEGER NOT NULL DEFAULT 0,
                html_content TEXT,
                has_image INTEGER NOT NULL DEFAULT 0,
                image_path TEXT,
                source_app TEXT,
                formats_json TEXT,
                text_sha1 TEXT,
                UNIQUE(timestamp, text_sha1)
            );
            CREATE INDEX IF NOT EXISTS idx_items_ts ON items(timestamp DESC);
            """);
    }

    public string DbPath => _dbPath;

    /// <summary>Directory for cached images captured from the clipboard.</summary>
    public string CacheDir { get; }

    // ── Write ───────────────────────────────────────────────────────────

    /// <summary>
    /// Inserts a capture, or merges it into an existing row sharing the same
    /// (timestamp, text hash). Returns true if a new row was created.
    /// </summary>
    public bool Upsert(StoreRow row)
    {
        lock (_gate)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO items
                    (timestamp, text_content, text_length, html_content, has_image,
                     image_path, source_app, formats_json, text_sha1)
                VALUES ($ts, $text, $len, $html, $img, $path, $src, $formats, $sha1)
                ON CONFLICT(timestamp, text_sha1) DO UPDATE SET
                    text_length = excluded.text_length,
                    html_content = COALESCE(excluded.html_content, items.html_content),
                    has_image = CASE WHEN excluded.has_image = 1 THEN 1 ELSE items.has_image END,
                    image_path = COALESCE(excluded.image_path, items.image_path),
                    source_app = COALESCE(NULLIF(excluded.source_app, ''), items.source_app),
                    formats_json = excluded.formats_json;
                """;
            cmd.Parameters.AddWithValue("$ts", row.Timestamp);
            cmd.Parameters.AddWithValue("$text", (object?)row.TextContent ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$len", row.TextLength);
            cmd.Parameters.AddWithValue("$html", (object?)row.HtmlContent ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$img", row.HasImage ? 1 : 0);
            cmd.Parameters.AddWithValue("$path", (object?)row.ImagePath ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$src", (object?)row.SourceApp ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$formats", JsonSerializer.Serialize(row.Formats));
            cmd.Parameters.AddWithValue("$sha1", TextHashFor(row.TextContent));
            return cmd.ExecuteNonQuery() > 0;
        }
    }

    // ── Read ────────────────────────────────────────────────────────────

    public IReadOnlyList<ClipItem> QueryPage(int limit, string? beforeTimestamp)
    {
        lock (_gate)
        {
            using var cmd = _conn.CreateCommand();
            if (beforeTimestamp is not null)
            {
                cmd.CommandText =
                    "SELECT * FROM items WHERE timestamp < $before ORDER BY timestamp DESC LIMIT $limit";
                cmd.Parameters.AddWithValue("$before", beforeTimestamp);
            }
            else
            {
                cmd.CommandText = "SELECT * FROM items ORDER BY timestamp DESC LIMIT $limit";
            }
            cmd.Parameters.AddWithValue("$limit", limit);

            var items = new List<ClipItem>();
            using var reader = cmd.ExecuteReader();
            var index = 0;
            while (reader.Read())
            {
                var item = RowToItem(reader, index);
                item = item with
                {
                    Kind = _detectors.Classify(item),
                    IsUserPinned = _meta.IsPinned(item.Timestamp),
                    IsSensitive = _meta.IsSensitive(item.Timestamp),
                    Alias = _meta.Alias(item.Timestamp),
                };
                items.Add(item);
                index++;
            }
            return items;
        }
    }

    // ── Retention / clear ───────────────────────────────────────────────

    public int SweepOlderThan(int? retentionDays)
    {
        if (retentionDays is null) return 0;
        var cutoff = DateTimeOffset.UtcNow.AddDays(-retentionDays.Value).ToString("o");
        lock (_gate)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM items WHERE timestamp < $cutoff";
            cmd.Parameters.AddWithValue("$cutoff", cutoff);
            var deleted = cmd.ExecuteNonQuery();
            if (deleted > 0) Exec("PRAGMA optimize;");
            return deleted;
        }
    }

    public int ClearAll()
    {
        lock (_gate)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM items";
            return cmd.ExecuteNonQuery();
        }
    }

    public string? GetImagePath(string timestamp)
    {
        lock (_gate)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT image_path FROM items WHERE timestamp = $ts AND image_path IS NOT NULL LIMIT 1";
            cmd.Parameters.AddWithValue("$ts", timestamp);
            return cmd.ExecuteScalar() as string;
        }
    }

    public bool DeleteByTimestamp(string timestamp)
    {
        lock (_gate)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM items WHERE timestamp = $ts";
            cmd.Parameters.AddWithValue("$ts", timestamp);
            var deleted = cmd.ExecuteNonQuery() > 0;
            if (deleted) _meta.Forget(timestamp);
            return deleted;
        }
    }

    // ── Settings ────────────────────────────────────────────────────────

    public ClipboardSettings LoadSettings()
    {
        try
        {
            var raw = File.ReadAllText(_settingsPath);
            var parsed = JsonSerializer.Deserialize<ClipboardSettings>(raw, JsonOpts);
            if (parsed is not null && ClipboardSettings.ValidRetentions.Contains(parsed.RetentionDays))
                return parsed;
        }
        catch
        {
            // Missing or corrupt file → defaults.
        }
        return new ClipboardSettings();
    }

    public void SaveSettings(ClipboardSettings settings)
    {
        File.WriteAllText(_settingsPath, JsonSerializer.Serialize(settings, JsonOpts));
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private void Exec(string sql)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static ClipItem RowToItem(SqliteDataReader r, int index)
    {
        var formatsJson = r["formats_json"] as string;
        IReadOnlyList<string> formats = [];
        if (!string.IsNullOrEmpty(formatsJson))
        {
            try
            {
                formats = JsonSerializer.Deserialize<List<string>>(formatsJson) ?? [];
            }
            catch
            {
                // corrupt row — empty format list
            }
        }

        return new ClipItem
        {
            Id = $"db:{index}",
            Timestamp = (string)r["timestamp"],
            Formats = formats,
            TextContent = r["text_content"] as string,
            TextLength = r["text_length"] is long len ? (int)len : 0,
            HtmlContent = r["html_content"] as string,
            HasImage = r["has_image"] is long hi && hi == 1,
            IsPinned = false,
            IsUserPinned = false,
            IsSensitive = false,
            SourceApp = r["source_app"] as string ?? "",
        };
    }

    // Empty string and null hash distinctly so same-timestamp items with
    // different formats do not collide on the UNIQUE key.
    private static string TextHashFor(string? textContent) =>
        Sha1(textContent ?? "__NULL__");

    private static string Sha1(string s) =>
        Convert.ToHexStringLower(SHA1.HashData(Encoding.UTF8.GetBytes(s)));

    public void Dispose() => _conn.Dispose();
}
