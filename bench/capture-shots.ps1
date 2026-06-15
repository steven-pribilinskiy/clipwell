#!/usr/bin/env pwsh
# Capture documentation screenshots of the Clipwell picker (compact + detail),
# settings, in both light and dark themes. Runs daemon + UI against an ISOLATED
# data dir and seeds items DIRECTLY via the dev /_seed endpoint, so it NEVER
# touches the user's real clipboard and the watcher stays off. The window is shown
# off-screen + non-activating (CLIPWELL_CAPTURE), so capture never steals focus.
#
# Run with Windows PowerShell (System.Drawing): powershell -File bench/capture-shots.ps1
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
$env:CLIPWELL_DATA_DIR    = $dataDir
$env:CLIPWELL_NO_SWEEP    = '1'
$env:CLIPWELL_NO_WATCH    = '1'   # never read the real clipboard
$env:CLIPWELL_ALLOW_SEED  = '1'   # enable the dev /_seed endpoint
$env:CLIPWELL_NO_AUTOHIDE = '1'
$env:CLIPWELL_CAPTURE     = '1'   # off-screen + non-activating (no focus steal)
$env:CLIPWELL_URL = $base
$env:CLIPWELL_API = $base

function Stop-Procs { Get-Process -Name 'Clipwell.Daemon','Clipwell.Ui' -EA SilentlyContinue | Where-Object { $_.Path -like "*\bin\Release\*" } | Stop-Process -Force -EA SilentlyContinue }
function Stop-Ui    { Get-Process -Name 'Clipwell.Ui' -EA SilentlyContinue | Where-Object { $_.Path -like "*\bin\Release\*" } | Stop-Process -Force -EA SilentlyContinue }

# PrintWindow capture (PW_RENDERFULLCONTENT = 2 grabs DWM content even off-screen).
Add-Type -ReferencedAssemblies 'System.Drawing','System.Drawing.Primitives' -TypeDefinition @"
using System; using System.Drawing; using System.Text; using System.Collections.Generic; using System.Runtime.InteropServices;
public static class Shot {
  [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr p);
  [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr h, IntPtr dc, uint f);
  [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
  delegate bool EnumProc(IntPtr h, IntPtr p);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L,T,R,B; }
  public static void DpiAware() { SetProcessDPIAware(); }
  // Find the visible top-level window with this title and a real size (skips the
  // daemon's 0x0 message-only window, also named "Clipwell").
  static IntPtr Find(string title) {
    IntPtr found = IntPtr.Zero;
    EnumWindows((h,p) => {
      if (!IsWindowVisible(h)) return true;
      var sb = new StringBuilder(256); GetWindowText(h, sb, 256);
      if (sb.ToString() == title) { RECT r; GetWindowRect(h, out r); if (r.R-r.L > 80) { found = h; return false; } }
      return true;
    }, IntPtr.Zero);
    return found;
  }
  public static bool Grab(string title, string path) {
    IntPtr h = Find(title);
    if (h == IntPtr.Zero) return false;
    RECT r; GetWindowRect(h, out r);
    int w = r.R - r.L, ht = r.B - r.T; if (w<=0||ht<=0) return false;
    using (var bmp = new Bitmap(w, ht)) using (var g = Graphics.FromImage(bmp)) {
      IntPtr dc = g.GetHdc(); bool ok = PrintWindow(h, dc, 2); g.ReleaseHdc(dc);
      if (!ok) return false;
      bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }
    return true;
  }
}
"@ 2>$null

[Shot]::DpiAware()
Stop-Procs; Start-Sleep -Seconds 1
try {
    Start-Process -FilePath $daemonExe -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $dataDir 'd.out') -RedirectStandardError (Join-Path $dataDir 'd.err')
    $up = $false
    foreach ($i in 1..30) { try { Invoke-RestMethod "$base/health" -TimeoutSec 2 | Out-Null; $up=$true; break } catch { Start-Sleep -Milliseconds 400 } }
    if (-not $up) { throw "daemon did not start" }

    # A sample image for the image item (gold ellipse on blue), written to a file.
    Add-Type -AssemblyName System.Drawing
    $bmp = New-Object System.Drawing.Bitmap 320,160
    $gfx = [System.Drawing.Graphics]::FromImage($bmp)
    $gfx.Clear([System.Drawing.Color]::FromArgb(60,90,200))
    $gfx.FillEllipse((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::Gold)), 120, 50, 80, 60)
    $gfx.Dispose()
    $imgPath = Join-Path $dataDir 'seed-img.png'
    $bmp.Save($imgPath, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()

    # Seed items directly (oldest first → newest shows on top), with varied source apps.
    function Seed($text, $hasImage, $img, $src) {
        $script:seedIdx = ($script:seedIdx + 1)
        $ts = $script:t0.AddSeconds($script:seedIdx).ToString("o")
        $body = @{ timestamp=$ts; text=$text; hasImage=[bool]$hasImage; imagePath=$img; sourceApp=$src } | ConvertTo-Json
        Invoke-RestMethod "$base/api/clipboard/_seed" -Method Post -ContentType 'application/json' -Body $body | Out-Null
    }
    $script:t0 = (Get-Date).ToUniversalTime(); $script:seedIdx = 0
    Seed 'The quick brown fox jumps over the lazy dog.' $false $null 'Notepad'
    Seed 'const sum = (a, b) => a + b;' $false $null 'VS Code'
    Seed 'C:\Users\you\Documents\report.txt' $false $null 'Explorer'
    Seed 'PROJ-1234' $false $null 'Chrome'
    Seed '#3366ff' $false $null 'Figma'
    Seed 'you@example.com' $false $null 'Outlook'
    Seed 'https://github.com/AvaloniaUI/Avalonia/pull/1234' $false $null 'Chrome'
    Seed 'https://avaloniaui.net' $false $null 'Chrome'
    Seed $null $true $imgPath 'Snipping Tool'

    # Pin the code snippet, flag the email as sensitive.
    $items = (Invoke-RestMethod "$base/api/clipboard?limit=50").items
    $code = $items | Where-Object { $_.kind -eq 'code' } | Select-Object -First 1
    $mail = $items | Where-Object { $_.kind -eq 'email' } | Select-Object -First 1
    if ($code) { Invoke-RestMethod "$base/api/clipboard/pin" -Method Post -ContentType 'application/json' -Body (@{timestamp=$code.timestamp;pinned=$true}|ConvertTo-Json) | Out-Null }
    if ($mail) { Invoke-RestMethod "$base/api/clipboard/sensitive" -Method Post -ContentType 'application/json' -Body (@{timestamp=$mail.timestamp;sensitive=$true}|ConvertTo-Json) | Out-Null }

    # Capture compact + detail picker and settings, in both themes.
    foreach ($theme in 'light','dark') {
        $env:CLIPWELL_THEME = $theme

        # Picker — compact
        $env:CLIPWELL_SHOW_SETTINGS = '0'; $env:CLIPWELL_QUICKLOOK = '0'; $env:CLIPWELL_VIEW = 'compact'
        Start-Process -FilePath $uiExe -WindowStyle Normal -RedirectStandardOutput (Join-Path $dataDir "u1-$theme.out") -RedirectStandardError (Join-Path $dataDir "u1-$theme.err")
        Start-Sleep -Seconds 7
        Write-Host "picker-$theme.png: $([Shot]::Grab('Clipwell', (Join-Path $outDir "picker-$theme.png")))"
        Stop-Ui; Start-Sleep -Seconds 2

        # Picker — detail
        $env:CLIPWELL_VIEW = 'detail'
        Start-Process -FilePath $uiExe -WindowStyle Normal -RedirectStandardOutput (Join-Path $dataDir "u2-$theme.out") -RedirectStandardError (Join-Path $dataDir "u2-$theme.err")
        Start-Sleep -Seconds 7
        Write-Host "detail-$theme.png: $([Shot]::Grab('Clipwell', (Join-Path $outDir "detail-$theme.png")))"
        Stop-Ui; Start-Sleep -Seconds 2
        $env:CLIPWELL_VIEW = 'compact'

        # Grouped by source
        $env:CLIPWELL_GROUP = 'source'
        Start-Process -FilePath $uiExe -WindowStyle Normal -RedirectStandardOutput (Join-Path $dataDir "ug-$theme.out") -RedirectStandardError (Join-Path $dataDir "ug-$theme.err")
        Start-Sleep -Seconds 7
        Write-Host "grouped-$theme.png: $([Shot]::Grab('Clipwell', (Join-Path $outDir "grouped-$theme.png")))"
        Stop-Ui; Start-Sleep -Seconds 2
        $env:CLIPWELL_GROUP = ''

        # Quick Look overlay (Ctrl+Y)
        $env:CLIPWELL_QUICKLOOK = '1'
        Start-Process -FilePath $uiExe -WindowStyle Normal -RedirectStandardOutput (Join-Path $dataDir "uq-$theme.out") -RedirectStandardError (Join-Path $dataDir "uq-$theme.err")
        Start-Sleep -Seconds 7
        Write-Host "quicklook-$theme.png: $([Shot]::Grab('Clipwell', (Join-Path $outDir "quicklook-$theme.png")))"
        Stop-Ui; Start-Sleep -Seconds 2
        $env:CLIPWELL_QUICKLOOK = '0'

        # Settings
        $env:CLIPWELL_SHOW_SETTINGS = '1'
        Start-Process -FilePath $uiExe -WindowStyle Normal -RedirectStandardOutput (Join-Path $dataDir "u3-$theme.out") -RedirectStandardError (Join-Path $dataDir "u3-$theme.err")
        Start-Sleep -Seconds 7
        Write-Host "settings-$theme.png: $([Shot]::Grab('Clipwell Settings', (Join-Path $outDir "settings-$theme.png")))"
        Stop-Ui; Start-Sleep -Seconds 2
        $env:CLIPWELL_SHOW_SETTINGS = '0'
    }
}
finally {
    Stop-Procs
    try { [System.IO.Directory]::Delete($dataDir, $true) } catch {}
}
