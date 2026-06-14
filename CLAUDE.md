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
- `GET  /api/clipboard/image/{timestamp}` → PNG for image items
- `GET  /api/clipboard/stream` (SSE), `GET /api/clipboard/ws` (WebSocket) — both
  emit `{ type:"clipboard.changed", timestamp, textLength }` on capture.
- `GET  /openapi/v1.json` — spec (mirrored to `openapi/clipwell.v1.json`).

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
`IClipboardWatcher` is the per-OS seam, selected by `ClipboardWatcherFactory`.
- `WindowsClipboardWatcher` — message-only window + `AddClipboardFormatListener`,
  pure Win32 P/Invoke. Captures text (CF_UNICODETEXT), HTML ("HTML Format"), and
  images (CF_DIB → PNG in the cache dir).
- `UnixPollingClipboardWatcher` — macOS/Linux, polls `pbpaste`/`wl-paste`/`xclip`
  (text-only; not yet exercised in CI).
- `NullClipboardWatcher` — fallback so the daemon runs everywhere.

Items are classified at read time by `DetectorRegistry` (`IClipDetector`s:
url/email/color/path/code/image/text).

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
appended to `perf.log` in the data dir. Measured (via `bench/run-bench.ps1`):
~170ms cold first show; **~16ms warm** hidden→shown cycle (≈ one 60Hz frame). The
synchronous render itself is ~1ms; the rest is the native show/activate/focus. See
ADR-0004. Per-OS global hotkey lives behind `IGlobalHotkey` (Windows done).

## Documentation — HARD RULE (update docs with every feature)
Documentation is part of "done", not a follow-up. For **every** user-visible feature
or behavior change:
1. Update the **feature docs** (`docs/content/docs/`) — the affected scenario/page,
   and add the feature to the relevant list. Refresh `llms.txt` is automatic.
2. Add or refresh **media**: a screenshot (and a short usage clip where it helps) of
   the new behavior, under `docs/public/media/`. Re-capture screenshots whenever the
   UI changes so they never go stale. Capture method: run the app against an isolated
   DB (`CLIPWELL_DATA_DIR`), use the picker, `PrintWindow` the window (works even when
   not foreground), or drive the docs site via Chrome.
3. Update the **engineering docs** (`engineering/content/docs/`) when architecture,
   the API surface, or a decision changes — and write a new **ADR** for any decision.
4. Update this **CLAUDE.md**, the **README**, and the **OpenAPI** spec if endpoints
   changed (`openapi/clipwell.v1.json`).
A PR/commit that ships a feature without its docs + media is incomplete.

## Profiling — HARD RULE (no regressions)
Profile on **every milestone/extension** and compare to the recorded baseline before
committing:
```sh
pwsh bench/run-bench.ps1        # measures REST latency + picker show-cycle
```
- Baseline lives at `bench/baseline.json`. The script prints current vs baseline and
  flags regressions (REST p95 or warm-show latency materially worse).
- If a change legitimately shifts the numbers, update the baseline in the same commit
  and note why. Never let the warm-show latency leave single digits (ms) without a
  recorded, justified reason — it is the product's core promise.
- Always run against an isolated `CLIPWELL_DATA_DIR`, never real history.

## Documentation sites
- `docs/` — user-facing feature docs (Fumadocs, Next.js static export). Landing page
  at `src/app/(home)/page.tsx`; content in `content/docs/`.
- `engineering/` — how-it-was-built site (same stack). Mermaid diagrams via the
  client `Mermaid` component; ADRs are a content section.
- Both deploy via `.github/workflows/docs.yml` to GitHub Pages: feature site at
  `/clipwell`, engineering at `/clipwell/engineering`. `PAGES_BASE` sets the subpath
  (empty for local builds). Build locally with `npm run build` in each.
