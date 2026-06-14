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

    public static void RecordShow(double milliseconds)
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            File.AppendAllText(Path, $"{DateTimeOffset.UtcNow:o}\tshow\t{milliseconds:F2}ms{Environment.NewLine}");
        }
        catch
        {
            // perf logging must never affect the UX
        }
    }
}
