#!/usr/bin/env pwsh
# Windows smoke test for the Clipwell daemon (CI). Mirrors scripts/ci-smoke.sh:
# starts the built daemon against an isolated DB, checks /health and the REST
# history endpoint, then does a clipboard round-trip to exercise the Win32
# WindowsClipboardWatcher.
$ErrorActionPreference = 'Stop'
$dll  = 'daemon/bin/Release/net10.0/Clipwell.Daemon.dll'
$port = 8799
$base = "http://127.0.0.1:$port"

$dataDir = Join-Path ([System.IO.Path]::GetTempPath()) "clipwell-ci-$(Get-Random)"
New-Item -ItemType Directory -Force -Path $dataDir | Out-Null
$env:CLIPWELL_DATA_DIR = $dataDir
$env:CLIPWELL_NO_SWEEP  = '1'
$env:CLIPWELL_URL = $base

$proc = Start-Process -FilePath 'dotnet' -ArgumentList $dll -PassThru -WindowStyle Hidden
try {
    $up = $false
    foreach ($i in 1..40) { try { Invoke-RestMethod "$base/health" -TimeoutSec 2 | Out-Null; $up=$true; break } catch { Start-Sleep -Milliseconds 500 } }
    if (-not $up) { throw "daemon did not become healthy" }

    $health = Invoke-RestMethod "$base/health"
    if ($health.status -ne 'ok') { throw "health not ok: $($health | ConvertTo-Json -Compress)" }
    Write-Host "health OK"

    $page = Invoke-RestMethod "$base/api/clipboard?limit=10"
    if ($null -eq $page.items) { throw "/api/clipboard missing items" }
    Write-Host "REST history OK"

    $marker = "clipwell-ci-roundtrip-$([System.Guid]::NewGuid().ToString('N'))"
    Set-Clipboard $marker
    $found = $false
    foreach ($i in 1..20) {
        Start-Sleep -Milliseconds 500
        if ((Invoke-RestMethod "$base/api/clipboard?limit=20").items.textContent -contains $marker) { $found=$true; break }
    }
    if (-not $found) { throw "clipboard round-trip — marker never captured" }
    Write-Host "clipboard round-trip OK"
    Write-Host "SMOKE PASSED"
}
finally {
    if ($proc -and -not $proc.HasExited) { $proc.Kill() }
    try { Remove-Item -Recurse -Force $dataDir -EA SilentlyContinue } catch {}
}
