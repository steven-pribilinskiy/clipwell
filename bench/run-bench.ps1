#!/usr/bin/env pwsh
# Clipwell performance bench. Measures daemon REST latency and (on a desktop
# session) the picker's warm show-cycle, then compares to bench/baseline.json and
# flags regressions. Always runs against an isolated DB — never real history.
#
#   pwsh bench/run-bench.ps1                 # measure + compare
#   pwsh bench/run-bench.ps1 -UpdateBaseline # measure + write baseline.json
#   pwsh bench/run-bench.ps1 -SkipUi         # REST only (e.g. headless/CI)

param(
    [switch]$UpdateBaseline,
    [switch]$SkipUi
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$daemonExe = Join-Path $repo 'daemon\bin\Release\net10.0\Clipwell.Daemon.exe'
$uiExe = Join-Path $repo 'ui\bin\Release\net10.0\Clipwell.Ui.exe'
$baselinePath = Join-Path $PSScriptRoot 'baseline.json'
$lastRunPath = Join-Path $PSScriptRoot 'last-run.json'
$port = 8788  # bench port, off the default 8787 so it never hits a real daemon
$base = "http://127.0.0.1:$port"

function Stop-BenchProcs {
    Get-Process -Name 'Clipwell.Daemon', 'Clipwell.Ui' -EA SilentlyContinue |
        Where-Object { $_.Path -like "*\bin\Release\*" } | Stop-Process -Force -EA SilentlyContinue
}

function Percentile([double[]]$values, [double]$p) {
    $sorted = $values | Sort-Object
    if ($sorted.Count -eq 0) { return 0 }
    $idx = [Math]::Ceiling($p / 100 * $sorted.Count) - 1
    return [Math]::Round($sorted[[Math]::Max(0, $idx)], 2)
}

if (-not (Test-Path $daemonExe)) {
    Write-Host "Building Release..." -ForegroundColor Cyan
    dotnet build (Join-Path $repo 'clipwell.slnx') -c Release -v quiet | Out-Null
}

$dataDir = Join-Path $env:TEMP "clipwell-bench-$(Get-Random)"
New-Item -ItemType Directory -Force -Path $dataDir | Out-Null
$env:CLIPWELL_DATA_DIR = $dataDir
$env:CLIPWELL_NO_SWEEP = '1'
$env:CLIPWELL_URL = $base
$env:CLIPWELL_API = $base
# NOTE: auto-hide stays ON for the bench so each hotkey press is a real
# hidden->shown cycle (we send Escape between presses to hide).

Stop-BenchProcs
Start-Sleep -Seconds 1

$result = [ordered]@{}
try {
    Start-Process -FilePath $daemonExe -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $dataDir 'd.out') `
        -RedirectStandardError (Join-Path $dataDir 'd.err')
    # wait for health
    $up = $false
    foreach ($i in 1..30) {
        try { Invoke-RestMethod "$base/health" -TimeoutSec 2 | Out-Null; $up = $true; break } catch { Start-Sleep -Milliseconds 500 }
    }
    if (-not $up) { throw "daemon did not start on $base" }

    # seed history
    1..50 | ForEach-Object { Set-Clipboard "bench item $_ $(Get-Random)"; Start-Sleep -Milliseconds 20 }
    Start-Sleep -Seconds 1

    # ---- REST latency: GET /api/clipboard?limit=200 ----
    $warmup = 5; $iters = 100
    1..$warmup | ForEach-Object { Invoke-RestMethod "$base/api/clipboard?limit=200" -TimeoutSec 5 | Out-Null }
    $samples = foreach ($i in 1..$iters) {
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        Invoke-RestMethod "$base/api/clipboard?limit=200" -TimeoutSec 5 | Out-Null
        $sw.Stop(); $sw.Elapsed.TotalMilliseconds
    }
    $result.rest_get_history_ms = [ordered]@{
        p50 = (Percentile $samples 50); p95 = (Percentile $samples 95); max = (Percentile $samples 100)
    }

    # ---- Picker warm show-cycle (desktop only) ----
    if (-not $SkipUi -and (Test-Path $uiExe)) {
        $perf = Join-Path $dataDir 'perf.log'
        Start-Process -FilePath $uiExe -WindowStyle Hidden `
            -RedirectStandardOutput (Join-Path $dataDir 'ui.out') -RedirectStandardError (Join-Path $dataDir 'ui.err')
        Start-Sleep -Seconds 8
        Add-Type @"
using System; using System.Runtime.InteropServices;
public class BenchKb { [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra); }
"@
        foreach ($i in 1..8) {
            # Alt+Shift+V → show (a real hidden->shown cycle)
            [BenchKb]::keybd_event(0x12,0,0,[UIntPtr]::Zero); [BenchKb]::keybd_event(0x10,0,0,[UIntPtr]::Zero)
            [BenchKb]::keybd_event(0x56,0,0,[UIntPtr]::Zero); Start-Sleep -Milliseconds 40; [BenchKb]::keybd_event(0x56,0,2,[UIntPtr]::Zero)
            [BenchKb]::keybd_event(0x10,0,2,[UIntPtr]::Zero); [BenchKb]::keybd_event(0x12,0,2,[UIntPtr]::Zero)
            Start-Sleep -Milliseconds 500
            # Escape → hide, so the next press is again a real show
            [BenchKb]::keybd_event(0x1B,0,0,[UIntPtr]::Zero); [BenchKb]::keybd_event(0x1B,0,2,[UIntPtr]::Zero)
            Start-Sleep -Milliseconds 400
        }
        Start-Sleep -Seconds 1
        if (Test-Path $perf) {
            $shows = Get-Content $perf | Where-Object { $_ -match 'show\s' } |
                ForEach-Object { if ($_ -match 'show\s+([\d.,]+)ms') { [double](($matches[1]) -replace ',', '.') } }
            # drop the first (cold) show; median the warm ones
            $warm = @($shows | Select-Object -Skip 1)
            if ($warm.Count -gt 0) {
                $result.picker_warm_show_ms = [ordered]@{ median = (Percentile $warm 50); count = $warm.Count }
            }
        }
    }
}
finally {
    Stop-BenchProcs
    try { [System.IO.Directory]::Delete($dataDir, $true) } catch {}
}

$result.recordedAt = (Get-Date).ToString('o')
$json = $result | ConvertTo-Json -Depth 5
Set-Content -Path $lastRunPath -Value $json -Encoding UTF8

Write-Host "`n=== Clipwell bench ===" -ForegroundColor Cyan
Write-Host $json

# ---- Compare to baseline ----
if ($UpdateBaseline) {
    Set-Content -Path $baselinePath -Value $json -Encoding UTF8
    Write-Host "`nBaseline updated." -ForegroundColor Green
    return
}
if (-not (Test-Path $baselinePath)) {
    Write-Host "`nNo baseline yet. Run with -UpdateBaseline to set one." -ForegroundColor Yellow
    return
}

$baseline = Get-Content $baselinePath -Raw | ConvertFrom-Json
$regressed = $false
function Compare-Metric($name, $cur, $base) {
    if ($null -eq $cur -or $null -eq $base) { return }
    # tolerance: 50% worse + 2ms absolute slack
    $threshold = $base * 1.5 + 2
    $status = if ($cur -gt $threshold) { $script:regressed = $true; 'REGRESSION' } else { 'ok' }
    $color = if ($status -eq 'REGRESSION') { 'Red' } else { 'Green' }
    Write-Host ("  {0,-28} {1,8:N2}  (baseline {2,8:N2})  {3}" -f $name, $cur, $base, $status) -ForegroundColor $color
}
Write-Host "`n=== vs baseline ===" -ForegroundColor Cyan
Compare-Metric 'rest p50 (ms)' $result.rest_get_history_ms.p50 $baseline.rest_get_history_ms.p50
Compare-Metric 'rest p95 (ms)' $result.rest_get_history_ms.p95 $baseline.rest_get_history_ms.p95
if ($result.picker_warm_show_ms) {
    Compare-Metric 'warm show median (ms)' $result.picker_warm_show_ms.median $baseline.picker_warm_show_ms.median
}
if ($regressed) { Write-Host "`nREGRESSION detected." -ForegroundColor Red; exit 1 }
Write-Host "`nNo regressions." -ForegroundColor Green
