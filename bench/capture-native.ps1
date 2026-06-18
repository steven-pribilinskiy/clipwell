#!/usr/bin/env pwsh
# macOS / Linux native-picker screenshots for ONE surface, captured with the OS's
# own window-grab tool (so we get the REAL composited window, exactly like PrintWindow
# does on Windows — no RenderTargetBitmap cropping). Orchestration is in pwsh so dates,
# JSON and HTTP behave identically on both runners.
#
#   Linux CI:  xvfb-run -a --server-args="-screen 0 1600x1200x24" pwsh bench/capture-native.ps1 linux
#   macOS CI:  pwsh bench/capture-native.ps1 macos
#
# Runs the daemon + UI as `dotnet <dll>` against an isolated, seeded DB (never the real
# clipboard), shows each state on-screen (CLIPWELL_NO_AUTOHIDE, no CLIPWELL_CAPTURE), and
# grabs the window into docs/public/media/shots/<state>/<surface>-<theme>.png.
#
# Requirements — Linux: xvfb, imagemagick (`import`), xdotool, wmctrl. macOS: built-in
# `screencapture` (needs screen-recording entitlement; CI runners usually grant it).
param(
  [Parameter(Mandatory)][ValidateSet('linux', 'macos')][string]$Surface
)
$ErrorActionPreference = 'Stop'
$repo      = Split-Path $PSScriptRoot -Parent
$daemonDll = Join-Path $repo 'daemon/bin/Release/net10.0/Clipwell.Daemon.dll'
$uiDll     = Join-Path $repo 'ui/bin/Release/net10.0/Clipwell.Ui.dll'
$shotsDir  = Join-Path $repo 'docs/public/media/shots'
$seedImg   = Join-Path $PSScriptRoot 'seed-sample.png'
$port = 8795
$base = "http://127.0.0.1:$port"
$dataDir = Join-Path ([System.IO.Path]::GetTempPath()) "clipwell-native-$(Get-Random)"
New-Item -ItemType Directory -Force -Path $dataDir | Out-Null

$env:CLIPWELL_DATA_DIR    = $dataDir
$env:CLIPWELL_NO_SWEEP    = '1'
$env:CLIPWELL_NO_WATCH    = '1'
$env:CLIPWELL_ALLOW_SEED  = '1'
$env:CLIPWELL_NO_AUTOHIDE = '1'   # keep the window up for the grab
$env:CLIPWELL_URL = $base
$env:CLIPWELL_API = $base
# Software rendering — CI runners have no GPU.
$env:LIBGL_ALWAYS_SOFTWARE = '1'

function Out-ShotPath($state, $theme) {
  $dir = Join-Path $shotsDir $state
  New-Item -ItemType Directory -Force -Path $dir | Out-Null
  return (Join-Path $dir "$Surface-$theme.png")
}

# Grab the window titled $title into $outfile using the OS tool. Returns $true on success.
function Grab($title, $outfile) {
  if ($Surface -eq 'linux') {
    $wid = $null
    foreach ($i in 1..30) {
      $wid = (& xdotool search --name "^$title$" 2>$null | Select-Object -First 1)
      if ($wid) { break }
      Start-Sleep -Milliseconds 500
    }
    if (-not $wid) { Write-Host "  ! window '$title' not found"; return $false }
    & xdotool windowactivate $wid 2>$null
    & xdotool windowraise $wid 2>$null
    Start-Sleep -Milliseconds 1000
    & import -window $wid -silent $outfile 2>$null
  }
  else {
    # macOS: grab the frontmost window (the picker is the only on-screen window).
    Start-Sleep -Milliseconds 1200
    $wid = (& osascript -e 'tell application "System Events" to id of front window of (first process whose frontmost is true)' 2>$null)
    if ($wid) { & screencapture -x -o -l $wid $outfile 2>$null }
    else { & screencapture -x -o $outfile 2>$null }   # fallback: full display
  }
  return (Test-Path $outfile)
}

$daemon = Start-Process -FilePath 'dotnet' -ArgumentList $daemonDll -PassThru `
  -RedirectStandardOutput (Join-Path $dataDir 'd.out') -RedirectStandardError (Join-Path $dataDir 'd.err')
try {
  $up = $false
  foreach ($i in 1..60) {
    try { Invoke-RestMethod "$base/health" -TimeoutSec 2 | Out-Null; $up = $true; break }
    catch { Start-Sleep -Milliseconds 400 }
  }
  if (-not $up) { throw 'daemon did not start' }

  $t0 = (Get-Date).ToUniversalTime(); $script:idx = 0; $script:total = 9
  function Seed($text, $hasImage, $img, $src) {
    $script:idx++
    $ts = $t0.AddSeconds(-($script:total - $script:idx) * 40).ToString('o')
    $body = @{ timestamp = $ts; text = $text; hasImage = [bool]$hasImage; imagePath = $img; sourceApp = $src } | ConvertTo-Json
    Invoke-RestMethod "$base/api/clipboard/_seed" -Method Post -ContentType 'application/json' -Body $body | Out-Null
  }
  Seed 'The quick brown fox jumps over the lazy dog.' $false $null 'Notepad'
  Seed 'const sum = (a, b) => a + b;' $false $null 'VS Code'
  Seed 'C:\Users\you\Documents\report.txt' $false $null 'Explorer'
  Seed 'PROJ-1234' $false $null 'Chrome'
  Seed '#3366ff' $false $null 'Figma'
  Seed 'you@example.com' $false $null 'Outlook'
  Seed 'https://github.com/AvaloniaUI/Avalonia/pull/1234' $false $null 'Chrome'
  Seed 'https://avaloniaui.net' $false $null 'Chrome'
  Seed $null $true $seedImg 'Snipping Tool'

  $items = (Invoke-RestMethod "$base/api/clipboard?limit=50").items
  $code = $items | Where-Object { $_.kind -eq 'code' } | Select-Object -First 1
  $mail = $items | Where-Object { $_.kind -eq 'email' } | Select-Object -First 1
  if ($code) { Invoke-RestMethod "$base/api/clipboard/pin" -Method Post -ContentType 'application/json' -Body (@{timestamp = $code.timestamp; pinned = $true } | ConvertTo-Json) | Out-Null }
  if ($mail) { Invoke-RestMethod "$base/api/clipboard/sensitive" -Method Post -ContentType 'application/json' -Body (@{timestamp = $mail.timestamp; sensitive = $true } | ConvertTo-Json) | Out-Null }

  # state → (window title, extra env)
  $states = @(
    @{ name = 'picker';      title = 'Clipwell';             env = @{ CLIPWELL_VIEW = 'compact' } },
    @{ name = 'detail';      title = 'Clipwell';             env = @{ CLIPWELL_VIEW = 'detail' } },
    @{ name = 'grouped';     title = 'Clipwell';             env = @{ CLIPWELL_GROUP = 'source' } },
    @{ name = 'actions';     title = 'Clipwell';             env = @{ CLIPWELL_ACTIONS = '1' } },
    @{ name = 'quicklook';   title = 'Clipwell';             env = @{ CLIPWELL_QUICKLOOK = '1' } },
    @{ name = 'settings';    title = 'Clipwell Settings';    env = @{ CLIPWELL_SHOW_SETTINGS = '1' } },
    @{ name = 'diagnostics'; title = 'Clipwell Diagnostics'; env = @{ CLIPWELL_SHOW_DIAG = '1' } }
  )
  $allKeys = 'CLIPWELL_VIEW', 'CLIPWELL_GROUP', 'CLIPWELL_ACTIONS', 'CLIPWELL_QUICKLOOK', 'CLIPWELL_SHOW_SETTINGS', 'CLIPWELL_SHOW_DIAG'

  foreach ($theme in 'light', 'dark') {
    $env:CLIPWELL_THEME = $theme
    foreach ($s in $states) {
      foreach ($k in $allKeys) { Remove-Item "env:$k" -ErrorAction SilentlyContinue }
      foreach ($k in $s.env.Keys) { Set-Item "env:$k" $s.env[$k] }
      $ui = Start-Process -FilePath 'dotnet' -ArgumentList $uiDll -PassThru `
        -RedirectStandardOutput (Join-Path $dataDir "ui-$($s.name)-$theme.out") `
        -RedirectStandardError (Join-Path $dataDir "ui-$($s.name)-$theme.err")
      Start-Sleep -Seconds 5   # let history + favicons load and the window map
      $ok = Grab $s.title (Out-ShotPath $s.name $theme)
      Write-Host "$($s.name)/$Surface-$theme.png: $ok"
      if (-not $ui.HasExited) { $ui.Kill() }
      Start-Sleep -Milliseconds 800
    }
  }
}
finally {
  if ($daemon -and -not $daemon.HasExited) { $daemon.Kill() }
  try { Remove-Item -Recurse -Force $dataDir -ErrorAction SilentlyContinue } catch {}
}
