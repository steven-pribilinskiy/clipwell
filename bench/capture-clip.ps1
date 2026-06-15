#!/usr/bin/env pwsh
# Capture a short "filter as you type" usage clip of the Clipwell picker, in both
# theme variants, and encode each to a looping WebM under docs/public/media/.
# Runs against an ISOLATED data dir (never real history). Requires ffmpeg on PATH.
#
#   powershell -ExecutionPolicy Bypass -File bench/capture-clip.ps1
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$daemonExe = Join-Path $repo 'daemon\bin\Release\net10.0\Clipwell.Daemon.exe'
$uiExe     = Join-Path $repo 'ui\bin\Release\net10.0\Clipwell.Ui.exe'
$outDir    = Join-Path $repo 'docs\public\media'
$port = 8790
$base = "http://127.0.0.1:$port"

$ffmpeg = (Get-Command ffmpeg -EA SilentlyContinue).Source
if (-not $ffmpeg) { throw "ffmpeg not found on PATH" }

New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$dataDir = Join-Path $env:TEMP "clipwell-clip-$(Get-Random)"
New-Item -ItemType Directory -Force -Path $dataDir | Out-Null
$env:CLIPWELL_DATA_DIR  = $dataDir
$env:CLIPWELL_NO_SWEEP  = '1'
$env:CLIPWELL_NO_AUTOHIDE = '1'
$env:CLIPWELL_URL = $base
$env:CLIPWELL_API = $base
$env:CLIPWELL_SHOW_SETTINGS = '0'

Add-Type -ReferencedAssemblies 'System.Drawing','System.Drawing.Primitives' -TypeDefinition @"
using System; using System.Drawing; using System.Runtime.InteropServices; using System.Threading;
public static class Clip {
  [DllImport("user32.dll")] static extern IntPtr FindWindow(string c, string n);
  [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr h, IntPtr dc, uint f);
  [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte sc, uint f, UIntPtr e);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L,T,R,B; }
  public static void DpiAware() { SetProcessDPIAware(); }
  public static IntPtr Find(string t) { return FindWindow(null, t); }
  public static void Foreground(IntPtr h) { SetForegroundWindow(h); }
  public static void Tap(byte vk) {
    keybd_event(vk,0,0,UIntPtr.Zero); Thread.Sleep(30); keybd_event(vk,0,2,UIntPtr.Zero);
  }
  public static bool Grab(IntPtr h, string path) {
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

function Stop-Procs { Get-Process -Name 'Clipwell.Daemon','Clipwell.Ui' -EA SilentlyContinue | Where-Object { $_.Path -like "*\bin\Release\*" } | Stop-Process -Force -EA SilentlyContinue }
function Stop-Ui    { Get-Process -Name 'Clipwell.Ui' -EA SilentlyContinue | Where-Object { $_.Path -like "*\bin\Release\*" } | Stop-Process -Force -EA SilentlyContinue }

# The daemon watches the GLOBAL system clipboard, so stray copies from the machine
# leak into history during a capture. Delete anything that isn't one of our seeds
# (keep images) so docs media never shows the user's real clipboard content.
$script:SeedSet = @(
    'The quick brown fox jumps over the lazy dog.',
    'const sum = (a, b) => a + b;',
    'C:\Users\you\Documents\report.txt',
    '#3366ff',
    'you@example.com',
    'https://avaloniaui.net'
)
function Remove-Strays {
    try {
        $seen = @{}
        # Items come newest-first; keep the first occurrence of each seed (and one
        # image), delete strays and duplicates so the list is exactly the 7 seeds.
        foreach ($it in (Invoke-RestMethod "$base/api/clipboard?limit=200").items) {
            $del = $false
            if ($it.hasImage) {
                if ($seen.ContainsKey('__img__')) { $del = $true } else { $seen['__img__'] = 1 }
            } elseif ($script:SeedSet -notcontains $it.textContent) {
                $del = $true                                   # stray copy
            } elseif ($seen.ContainsKey($it.textContent)) {
                $del = $true                                   # duplicate seed
            } else {
                $seen[$it.textContent] = 1
            }
            if ($del) {
                Invoke-RestMethod "$base/api/clipboard/delete" -Method Post -ContentType 'application/json' -Body (@{timestamp=$it.timestamp}|ConvertTo-Json) | Out-Null
            }
        }
    } catch {}
}

function Set-ClipImage {
    $sb = {
        Add-Type -AssemblyName System.Windows.Forms,System.Drawing
        $bmp = New-Object System.Drawing.Bitmap 320,160
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.Clear([System.Drawing.Color]::FromArgb(60,90,200))
        $g.FillEllipse((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::Gold)), 120, 50, 80, 60)
        $g.Dispose()
        for ($i=0; $i -lt 8; $i++) { try { [System.Windows.Forms.Clipboard]::SetImage($bmp); break } catch { Start-Sleep -Milliseconds 150 } }
    }
    $ps = [PowerShell]::Create(); $ps.Runspace = [RunspaceFactory]::CreateRunspace()
    $ps.Runspace.ApartmentState = 'STA'; $ps.Runspace.Open(); $ps.AddScript($sb).Invoke() | Out-Null
    $ps.Runspace.Close(); $ps.Dispose()
}

[Clip]::DpiAware()
Stop-Procs; Start-Sleep -Seconds 1
try {
    Start-Process -FilePath $daemonExe -WindowStyle Hidden -RedirectStandardOutput (Join-Path $dataDir 'd.out') -RedirectStandardError (Join-Path $dataDir 'd.err')
    $up=$false; foreach ($i in 1..30) { try { Invoke-RestMethod "$base/health" -TimeoutSec 2 | Out-Null; $up=$true; break } catch { Start-Sleep -Milliseconds 400 } }
    if (-not $up) { throw "daemon did not start" }

    # Set-Clipboard can transiently fail if another app holds the clipboard — retry.
    function Set-ClipText($s) {
        for ($i=0; $i -lt 10; $i++) {
            try { Set-Clipboard $s -ErrorAction Stop; return } catch { Start-Sleep -Milliseconds 200 }
        }
    }
    foreach ($s in $script:SeedSet) { Set-ClipText $s; Start-Sleep -Milliseconds 250 }
    Set-ClipImage; Start-Sleep -Milliseconds 600

    # Pin the code snippet + flag the email sensitive, matching the static shots.
    $items = (Invoke-RestMethod "$base/api/clipboard?limit=50").items
    $code = $items | Where-Object { $_.kind -eq 'code' } | Select-Object -First 1
    $mail = $items | Where-Object { $_.kind -eq 'email' } | Select-Object -First 1
    if ($code) { Invoke-RestMethod "$base/api/clipboard/pin" -Method Post -ContentType 'application/json' -Body (@{timestamp=$code.timestamp;pinned=$true}|ConvertTo-Json) | Out-Null }
    if ($mail) { Invoke-RestMethod "$base/api/clipboard/sensitive" -Method Post -ContentType 'application/json' -Body (@{timestamp=$mail.timestamp;sensitive=$true}|ConvertTo-Json) | Out-Null }

    Remove-Strays  # dedup/strays from seeding to exactly the 7 seeds

    # Restart the daemon with the watcher OFF so the user's live clipboard activity
    # can't leak into the capture. The seeded SQLite history persists in $dataDir.
    Stop-Procs; Start-Sleep -Seconds 1
    $env:CLIPWELL_NO_WATCH = '1'
    Start-Process -FilePath $daemonExe -WindowStyle Hidden -RedirectStandardOutput (Join-Path $dataDir 'd2.out') -RedirectStandardError (Join-Path $dataDir 'd2.err')
    $up=$false; foreach ($i in 1..30) { try { Invoke-RestMethod "$base/health" -TimeoutSec 2 | Out-Null; $up=$true; break } catch { Start-Sleep -Milliseconds 400 } }
    if (-not $up) { throw "daemon (no-watch) did not restart" }

    foreach ($theme in 'light','dark') {
        $env:CLIPWELL_THEME = $theme
        $frames = Join-Path $dataDir "f-$theme"; New-Item -ItemType Directory -Force -Path $frames | Out-Null
        Start-Process -FilePath $uiExe -WindowStyle Normal -RedirectStandardOutput (Join-Path $dataDir "ui-$theme.out") -RedirectStandardError (Join-Path $dataDir "ui-$theme.err")
        Start-Sleep -Seconds 7
        $h = [Clip]::Find('Clipwell')
        [Clip]::Foreground($h); Start-Sleep -Milliseconds 400

        $n = 0
        function Frame { param($h,$frames) ; $p = Join-Path $frames ("{0:000}.png" -f $script:n); [Clip]::Grab($h,$p) | Out-Null; $script:n++ }
        # 0: full list
        Frame $h $frames; Frame $h $frames
        # type "av" → filters to avaloniaui.net
        [Clip]::Tap(0x41); Start-Sleep -Milliseconds 350; Frame $h $frames           # a
        [Clip]::Tap(0x56); Start-Sleep -Milliseconds 350; Frame $h $frames; Frame $h $frames  # v (hold)
        Frame $h $frames
        # clear (backspace x2) → back to full list
        [Clip]::Tap(0x08); Start-Sleep -Milliseconds 300; Frame $h $frames           # backspace
        [Clip]::Tap(0x08); Start-Sleep -Milliseconds 300; Frame $h $frames; Frame $h $frames  # backspace (hold)

        Stop-Ui; Start-Sleep -Seconds 1

        # Encode: 2.5 fps input (each state ~0.4s), smooth 30fps output, looping in <video>.
        # ffmpeg writes its banner to stderr; relax the error pref so that's not fatal.
        $webm = Join-Path $outDir "usage-$theme.webm"
        $prevEAP = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
        & $ffmpeg -y -framerate 2.5 -i (Join-Path $frames '%03d.png') `
            -c:v libvpx-vp9 -pix_fmt yuv420p -b:v 0 -crf 34 -r 30 `
            -vf "scale=trunc(iw/2)*2:trunc(ih/2)*2" $webm *>$null
        $ErrorActionPreference = $prevEAP
        Write-Host "usage-$theme.webm: exists=$(Test-Path $webm) frames=$n ffmpegExit=$LASTEXITCODE"
    }
}
finally {
    Stop-Procs
    try { [System.IO.Directory]::Delete($dataDir, $true) } catch {}
}
