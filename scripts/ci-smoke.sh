#!/usr/bin/env bash
# Cross-platform smoke test for the Clipwell daemon (Linux + macOS CI).
# Starts the built daemon against an isolated DB, checks /health and the REST
# history endpoint, then does a real clipboard round-trip through the platform's
# native clipboard CLI to exercise UnixPollingClipboardWatcher.
#
# Linux requires an X server for xclip — run under xvfb-run. macOS uses pbcopy/
# pbpaste natively.
set -euo pipefail

DLL="daemon/bin/Release/net10.0/Clipwell.Daemon.dll"
PORT=8799
BASE="http://127.0.0.1:${PORT}"

DATA_DIR="$(mktemp -d)"
export CLIPWELL_DATA_DIR="$DATA_DIR"
export CLIPWELL_NO_SWEEP=1
export CLIPWELL_URL="$BASE"
trap 'kill "${DAEMON_PID:-0}" 2>/dev/null || true; rm -rf "$DATA_DIR"' EXIT

echo "::group::start daemon"
dotnet "$DLL" &
DAEMON_PID=$!
echo "daemon pid=$DAEMON_PID  data=$DATA_DIR"
echo "::endgroup::"

# Wait for health.
up=""
for _ in $(seq 1 40); do
  if curl -fsS "$BASE/health" >/dev/null 2>&1; then up=1; break; fi
  sleep 0.5
done
[ -n "$up" ] || { echo "FAIL: daemon did not become healthy"; exit 1; }

health="$(curl -fsS "$BASE/health")"
echo "health: $health"
case "$health" in *'"status":"ok"'*) ;; *) echo "FAIL: health not ok"; exit 1;; esac

# REST history shape.
page="$(curl -fsS "$BASE/api/clipboard?limit=10")"
case "$page" in *'"items"'*) ;; *) echo "FAIL: /api/clipboard missing items"; exit 1;; esac
echo "REST history OK"

# Clipboard round-trip through the native CLI.
MARKER="clipwell-ci-roundtrip-$$-${RANDOM:-0}"
uname_s="$(uname -s)"
if [ "$uname_s" = "Darwin" ]; then
  printf '%s' "$MARKER" | pbcopy
else
  # Linux: prefer wl-copy (Wayland), fall back to xclip (X11, needs a display).
  if command -v wl-copy >/dev/null 2>&1 && [ -n "${WAYLAND_DISPLAY:-}" ]; then
    printf '%s' "$MARKER" | wl-copy
  else
    printf '%s' "$MARKER" | xclip -selection clipboard
  fi
fi

# Poll interval is 600ms; give it a few cycles.
found=""
for _ in $(seq 1 20); do
  sleep 0.5
  if curl -fsS "$BASE/api/clipboard?limit=20" | grep -q "$MARKER"; then found=1; break; fi
done
[ -n "$found" ] || { echo "FAIL: clipboard round-trip — marker never captured"; exit 1; }
echo "clipboard round-trip OK ($uname_s captured the marker)"

echo "SMOKE PASSED"
