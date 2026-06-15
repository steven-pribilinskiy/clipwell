using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;

namespace Clipwell.Ui;

public partial class DiagnosticsWindow : Window
{
    private readonly ClipwellClient _client = new();

    public DiagnosticsWindow()
    {
        InitializeComponent();
        if (Environment.GetEnvironmentVariable("CLIPWELL_CAPTURE") == "1")
        {
            ShowActivated = false;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Position = new PixelPoint(-4000, -4000);
        }
        Opened += (_, _) => Refresh();
        RefreshButton.Click += (_, _) => Refresh();
        CloseButton.Click += (_, _) => Close();
        ClearButton.Click += (_, _) => { PerfLog.Clear(); Refresh(); };
    }

    private async void Refresh()
    {
        var health = await _client.GetHealthAsync();
        DaemonText.Text = health is null
            ? "Unreachable — is the daemon running?"
            : $"status: {health.Status}\ndb: {health.Db}\nsubscribers: {health.Subscribers}";

        // perf.log lines: "<iso>\tshow\t<ms>ms" — show newest first with a summary.
        var lines = new List<string>();
        var ms = new List<double>();
        try
        {
            if (File.Exists(PerfLog.FilePath))
            {
                foreach (var raw in File.ReadLines(PerfLog.FilePath))
                {
                    var parts = raw.Split('\t');
                    if (parts.Length < 3) continue;
                    var msText = parts[2].Replace("ms", "").Trim();
                    if (double.TryParse(msText, NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
                    {
                        ms.Add(v);
                        var when = DateTimeOffset.TryParse(parts[0], CultureInfo.InvariantCulture,
                            DateTimeStyles.AssumeUniversal, out var t) ? t.LocalDateTime.ToString("HH:mm:ss") : parts[0];
                        lines.Add($"{when}   {v,7:F2} ms");
                    }
                }
            }
        }
        catch { /* best effort */ }

        lines.Reverse(); // newest first
        PerfList.ItemsSource = lines.Take(500).ToList();
        PerfSummary.Text = ms.Count == 0
            ? "No show-cycle samples yet."
            : $"{ms.Count} shows · median {Median(ms):F1} ms · min {ms.Min():F1} · max {ms.Max():F1}";
        StatusText.Text = $"perf.log: {PerfLog.FilePath}";
    }

    private static double Median(List<double> xs)
    {
        var s = xs.OrderBy(x => x).ToList();
        var n = s.Count;
        return n % 2 == 1 ? s[n / 2] : (s[n / 2 - 1] + s[n / 2]) / 2;
    }
}
