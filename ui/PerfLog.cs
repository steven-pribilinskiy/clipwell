using System;
using System.IO;

namespace Clipwell.Ui;

/// <summary>
/// Appends picker show-cycle timings to perf.log, so the engineering docs can chart
/// real latency numbers (the single-digit-ms bar). One line per show.
/// </summary>
public static class PerfLog
{
    private static readonly string Path = System.IO.Path.Combine(
        Environment.GetEnvironmentVariable("CLIPWELL_DATA_DIR")
            ?? System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Clipwell"),
        "perf.log");

    /// <summary>Absolute path to perf.log (for the diagnostics window).</summary>
    public static string FilePath => Path;

    /// <summary>Truncate the perf log.</summary>
    public static void Clear()
    {
        try { if (File.Exists(Path)) File.WriteAllText(Path, ""); } catch { /* ignore */ }
    }

    public static void RecordShow(double milliseconds)
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            // Invariant culture so the decimal point is stable across locales (the
            // diagnostics window parses with InvariantCulture).
            var ms = milliseconds.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
            File.AppendAllText(Path, $"{DateTimeOffset.UtcNow:o}\tshow\t{ms}ms{Environment.NewLine}");
        }
        catch
        {
            // perf logging must never affect the UX
        }
    }
}
