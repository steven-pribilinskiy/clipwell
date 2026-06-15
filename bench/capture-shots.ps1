#!/usr/bin/env pwsh
# Capture documentation screenshots of the Clipwell picker and Settings window.
# Runs daemon + UI against an ISOLATED data dir (never real history), seeds a
# diverse set of clipboard items, pins/flags a couple, then PrintWindows each
# window (works even when not foreground) into docs/public/media/.
#
#   pwsh bench/capture-shots.ps1
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$daemonExe = Join-Path $repo 'daemon\bin\Release\net10.0\Clipwell.Daemon.exe'
$uiExe     = Join-Path $repo 'ui\bin\Release\net10.0\Clipwell.Ui.exe'
$outDir    = Join-Path $repo 'docs\public\media'
$port = 8789
$base = "http://127.0.0.1:$port"

New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$dataDir = Join-Path $env:TEMP "clipwell-shots-$(Get-Random)"
New-Item -ItemType Directory -Force -Path $dataDir | Out-Null
$env:CLIPWELL_DATA_DIR  = $dataDir
$env:CLIPWELL_NO_SWEEP  = '1'
$env:CLIPWELL_NO_AUTOHIDE = '1'      # keep windows visible for capture
$env:CLIPWELL_CAPTURE = '1'          # off-screen + non-activating (never steals focus)
$env:CLIPWELL_URL = $base
$env:CLIPWELL_API = $base

function Stop-Procs {
    Get-Process -Name 'Clipwell.Daemon','Clipwell.Ui' -EA SilentlyContinue |
        Where-Object { $_.Path -like "*\bin\Release\*" } | Stop-Process -Force -EA SilentlyContinue
}
function Stop-Ui {
    # Stop only the picker — leave the daemon running across theme iterations.
    Get-Process -Name 'Clipwell.Ui' -EA SilentlyContinue |
        Where-Object { $_.Path -like "*\bin\Release\*" } | Stop-Process -Force -EA SilentlyContinue
}

# PrintWindow capture (PW_RENDERFULLCONTENT = 2 grabs DWM content even if occluded).
Add-Type -ReferencedAssemblies 'System.Drawing','System.Drawing.Primitives' -TypeDefinition @"
using System; using System.Drawing; using System.Runtime.InteropServices;
public static class Shot {
  [DllImport("user32.dll")] static extern IntPtr FindWindow(string c, string n);
  [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr h, IntPtr dc, uint f);
  [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
  // PrintWindow renders physical pixels; become DPI-aware so GetWindowRect agrees.
  public static void DpiAware() { SetProcessDPIAware(); }
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L,T,R,B; }
  public static bool Grab(string title, string path) {
    IntPtr h = FindWindow(null, title);
    if (h == IntPtr.Zero) return false;
    RECT r; GetWindowRect(h, out r);
    int w = r.R - r.L, ht = r.B - r.T;
    if (w <= 0 || ht <= 0) return false;
    using (var bmp = new Bitmap(w, ht))
    using (var g = Graphics.FromImage(bmp)) {
      IntPtr dc = g.GetHdc();
      bool ok = PrintWindow(h, dc, 2);
      g.ReleaseHdc(dc);
      if (!ok) return false;
      bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }
    return true;
  }
}
"@ 2>$null

# STA helper to push a bitmap onto the clipboard (image item).
function Set-ClipImage {
    $sb = {
        Add-Type -AssemblyName System.Windows.Forms,System.Drawing
        $bmp = New-Object System.Drawing.Bitmap 320,160
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.Clear([System.Drawing.Color]::FromArgb(60,90,200))
        $br = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::Gold)
        $g.FillEllipse($br, 120, 50, 80, 60)
        $g.Dispose()
        for ($i=0; $i -lt 8; $i++) {
            try { [System.Windows.Forms.Clipboard]::SetImage($bmp); break } catch { Start-Sleep -Milliseconds 150 }
        }
    }
    $ps = [PowerShell]::Create()
    $ps.Runspace = [RunspaceFactory]::CreateRunspace()
    $ps.Runspace.ApartmentState = 'STA'
    $ps.Runspace.Open()
    $ps.AddScript($sb).Invoke() | Out-Null
    $ps.Runspace.Close(); $ps.Dispose()
}

[Shot]::DpiAware()
Stop-Procs; Start-Sleep -Seconds 1
try {
    Start-Process -FilePath $daemonExe -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $dataDir 'd.out') -RedirectStandardError (Join-Path $dataDir 'd.err')
    $up = $false
    foreach ($i in 1..30) { try { Invoke-RestMethod "$base/health" -TimeoutSec 2 | Out-Null; $up=$true; break } catch { Start-Sleep -Milliseconds 400 } }
    if (-not $up) { throw "daemon did not start" }

    # Seed diverse items (oldest first so newest sits on top).
    $seed = @(
        'The quick brown fox jumps over the lazy dog.',
        'const sum = (a, b) => a + b;',
        'C:\Users\you\Documents\report.txt',
        'PROJ-1234',
        '#3366ff',
        'you@example.com',
        'https://github.com/AvaloniaUI/Avalonia/pull/1234',
        'https://avaloniaui.net'
    )
    # Set-Clipboard can transiently fail if another app holds the clipboard — retry.
    function Set-ClipText($s) {
        for ($i=0; $i -lt 10; $i++) {
            try { Set-Clipboard $s -ErrorAction Stop; return } catch { Start-Sleep -Milliseconds 200 }
        }
    }
    foreach ($s in $seed) { Set-ClipText $s; Start-Sleep -Milliseconds 250 }
    Set-ClipImage; Start-Sleep -Milliseconds 600

    # The daemon watches the GLOBAL clipboard, so stray copies from the machine can
    # leak in during a capture. Delete anything that isn't a seed (keep images) so
    # docs media never shows the user's real clipboard content.
    function Remove-Strays {
        try {
            $seen = @{}
            foreach ($it in (Invoke-RestMethod "$base/api/clipboard?limit=200").items) {
                $del = $false
                if ($it.hasImage) {
                    if ($seen.ContainsKey('__img__')) { $del = $true } else { $seen['__img__'] = 1 }
                } elseif ($seed -notcontains $it.textContent) {
                    $del = $true
                } elseif ($seen.ContainsKey($it.textContent)) {
                    $del = $true
                } else {
                    $seen[$it.textContent] = 1
                }
                if ($del) {
                    Invoke-RestMethod "$base/api/clipboard/delete" -Method Post -ContentType 'application/json' -Body (@{timestamp=$it.timestamp}|ConvertTo-Json) | Out-Null
                }
            }
        } catch {}
    }

    # Pin the code snippet, flag the email as sensitive (use their timestamps).
    $items = (Invoke-RestMethod "$base/api/clipboard?limit=50").items
    $code = $items | Where-Object { $_.kind -eq 'code' } | Select-Object -First 1
    $mail = $items | Where-Object { $_.kind -eq 'email' } | Select-Object -First 1
    if ($code) { Invoke-RestMethod "$base/api/clipboard/pin" -Method Post -ContentType 'application/json' -Body (@{timestamp=$code.timestamp;pinned=$true}|ConvertTo-Json) | Out-Null }
    if ($mail) { Invoke-RestMethod "$base/api/clipboard/sensitive" -Method Post -ContentType 'application/json' -Body (@{timestamp=$mail.timestamp;sensitive=$true}|ConvertTo-Json) | Out-Null }

    Remove-Strays  # dedup/strays from seeding to exactly the 7 seeds

    # Restart the daemon with the watcher OFF so the user's live clipboard activity
    # can't leak into the screenshots. The seeded SQLite history persists in $dataDir.
    Stop-Procs; Start-Sleep -Seconds 1
    $env:CLIPWELL_NO_WATCH = '1'
    Start-Process -FilePath $daemonExe -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $dataDir 'd2.out') -RedirectStandardError (Join-Path $dataDir 'd2.err')
    $up = $false
    foreach ($i in 1..30) { try { Invoke-RestMethod "$base/health" -TimeoutSec 2 | Out-Null; $up=$true; break } catch { Start-Sleep -Milliseconds 400 } }
    if (-not $up) { throw "daemon (no-watch) did not restart" }

    # Capture the picker and the settings window in BOTH theme variants. The docs
    # site is light/dark (system default), so screenshots ship for each theme.
    $n = 0
    foreach ($theme in 'light','dark') {
        $env:CLIPWELL_THEME = $theme

        # ---- Picker (compact) ----
        $env:CLIPWELL_SHOW_SETTINGS = '0'
        $env:CLIPWELL_VIEW = 'compact'
        Start-Process -FilePath $uiExe -WindowStyle Normal `
            -RedirectStandardOutput (Join-Path $dataDir "ui-$theme.out") -RedirectStandardError (Join-Path $dataDir "ui-$theme.err")
        Start-Sleep -Seconds 7
        $ok = [Shot]::Grab('Clipwell', (Join-Path $outDir "picker-$theme.png"))
        Write-Host "picker-$theme.png: $ok"
        Stop-Ui; Start-Sleep -Seconds 2

        # ---- Picker (detail view) ----
        $env:CLIPWELL_VIEW = 'detail'
        Start-Process -FilePath $uiExe -WindowStyle Normal `
            -RedirectStandardOutput (Join-Path $dataDir "uid-$theme.out") -RedirectStandardError (Join-Path $dataDir "uid-$theme.err")
        Start-Sleep -Seconds 7
        $okd = [Shot]::Grab('Clipwell', (Join-Path $outDir "detail-$theme.png"))
        Write-Host "detail-$theme.png: $okd"
        Stop-Ui; Start-Sleep -Seconds 2
        $env:CLIPWELL_VIEW = 'compact'

        # ---- Settings ----
        $env:CLIPWELL_SHOW_SETTINGS = '1'
        Start-Process -FilePath $uiExe -WindowStyle Normal `
            -RedirectStandardOutput (Join-Path $dataDir "ui2-$theme.out") -RedirectStandardError (Join-Path $dataDir "ui2-$theme.err")
        Start-Sleep -Seconds 7
        $ok2 = [Shot]::Grab('Clipwell Settings', (Join-Path $outDir "settings-$theme.png"))
        Write-Host "settings-$theme.png: $ok2"
        Stop-Ui; Start-Sleep -Seconds 2
    }
}
finally {
    Stop-Procs
    try { [System.IO.Directory]::Delete($dataDir, $true) } catch {}
}
