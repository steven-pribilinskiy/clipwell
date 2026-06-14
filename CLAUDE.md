# Clipwell

Cross-platform clipboard-history product: a headless **daemon** plus thin clients
(picker UI, CLI, MCP), all speaking one public API (REST + WebSocket/SSE + MCP).
Extracted from `windows-settings/clipwell` (a Windows-only WPF app) as a clean
rewrite — the WPF code is reference only, kept in the windows-settings repo.

## Stack
- **.NET 10**, C#. Solution: `clipwell.slnx` (note: new XML `.slnx`, not `.sln`).
- `protocol/` — domain model (`ClipItem`, `StoreRow`, `ClipboardSettings`) + plugin
  contracts (`IClipDetector`, `IClipAction`). No dependencies.
- `daemon/` — ASP.NET minimal API (Kestrel). SQLite via `Microsoft.Data.Sqlite`.
- `ui/` — Avalonia 12 picker (thin client; fetches `/api/clipboard`, live-updates
  over the WS). MVVM via CommunityToolkit.Mvvm. See ADR-0002.
- `cli/` — reference API consumer.
- `mcp/` — stdio MCP server (ModelContextProtocol SDK) exposing clipboard tools;
  proxies to the daemon over REST. See ADR-0003.
- `openapi/clipwell.v1.json` — checked-in spec, regenerated from `GET /openapi/v1.json`.
- `engineering/content/adr/` — Architecture Decision Records (MADR). Write one per
  decision, at decision time (docs-as-you-build).

## Run
```sh
dotnet build clipwell.slnx
dotnet run --project daemon          # listens on http://127.0.0.1:8787
```
Default port **8787**, override with `CLIPWELL_URL`. CLI base URL override:
`CLIPWELL_API`.

## API (Phase 1)
- `GET  /health`
- `GET  /api/clipboard?limit=&before=` → `{ items: ClipItem[] }`
- `GET|POST /api/clipboard/settings`
- `POST /api/clipboard/delete` `{ timestamp }`, `POST /api/clipboard/clear`
- `GET  /api/clipboard/stream` (SSE), `GET /api/clipboard/ws` (WebSocket) — both
  emit `{ type:"clipboard.changed", timestamp, textLength }` on capture.

## Storage
SQLite at `%APPDATA%\Roaming\Clipwell\history.db` (Windows) / `~/.config/Clipwell`
(Linux) / `~/Library/Application Support/Clipwell` (macOS). Schema ported verbatim
from the old `clipboard-store.ts` so existing history files keep working.

## DEV SAFETY — read before testing locally
- The default DB path is the **user's real clipboard history**, shared with the
  legacy WPF Clipwell app if it's still installed. **Always set
  `CLIPWELL_DATA_DIR` to a temp folder when running dev/test builds** so you never
  read or mutate real history. (The retention sweep is destructive.)
- The retention sweep now runs an hour *after* startup (not on boot), so a
  short-lived dev run won't purge anything — but isolation via `CLIPWELL_DATA_DIR`
  is still the rule.
- **Killing the daemon:** `dotnet run` launches a separate `Clipwell.Daemon.exe`
  apphost — it is NOT a `dotnet.exe` process. Kill it with
  `Stop-Process -Name Clipwell.Daemon -Force`, then confirm port 8787 is free,
  *before* rebuilding. A live daemon holds a lock on the output DLL/exe, so a
  rebuild over it silently leaves a stale binary running (this wastes a lot of
  debugging time — verify the DLL `LastWriteTime` advanced after a build).

## Platform watchers
`IClipboardWatcher` is the per-OS seam. Implemented: `WindowsClipboardWatcher`
(message-only window + `AddClipboardFormatListener`, pure Win32 P/Invoke, captures
`CF_UNICODETEXT`). `NullClipboardWatcher` is the fallback so the daemon runs
everywhere. macOS (NSPasteboard polling) and Linux (X11/Wayland) are TODO, as are
image/HTML capture on Windows.

## Avalonia 12 gotchas
- Clipboard: `DataObject`/`DataFormats` are obsolete → `DataTransfer`/`DataFormat`.
  For plain text, `IClipboard.SetTextAsync` is an extension in
  `Avalonia.Input.Platform` (add the using).
- `TextBox.Watermark` → `PlaceholderText`.
- Solution file is `.slnx` (XML), not `.sln`.

## Perf bar
The picker reaches single-digit-ms visibility via a pre-warmed window shown on a
global hotkey (Alt+Shift+V on Windows; Win+V is OS-reserved). Background app
(tray icon, OnExplicitShutdown), single-instance via named mutex, hide-on-blur
(disable with `CLIPWELL_NO_AUTOHIDE` for screenshot tests). Show-cycle latency is
appended to `perf.log` in the data dir. Measured: ~170ms cold first show, ~6ms warm.
See ADR-0004. Per-OS global hotkey lives behind `IGlobalHotkey` (Windows done).
