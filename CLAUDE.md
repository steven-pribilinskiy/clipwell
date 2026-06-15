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
- `POST /mcp` — MCP over Streamable HTTP/SSE, served in-process by the daemon
  (`AddMcpServer().WithHttpTransport()`, `DaemonClipboardTools` hits the store
  directly). Same four tools as the stdio server in `mcp/`. Not in the OpenAPI
  spec — MCP is its own JSON-RPC protocol.

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
  (text-only; smoke-tested on macOS + Linux CI via a clipboard round-trip).
- `NullClipboardWatcher` — fallback so the daemon runs everywhere.

Items are classified at read time by `DetectorRegistry` (`IClipDetector`s:
url/github-pr/email/jira-issue/color/path/code/image/text).

## Plugins (B2)
Two contracts in `Clipwell.Protocol.Plugins`: `IClipDetector` (custom kinds, loaded by
the **daemon**) and `IClipAction` (Ctrl+K palette actions, loaded by the **picker**),
the latter via `IClipActionContext` (OpenUrl/OpenPath/SetClipboard/Notify) so actions
stay UI-free. `PluginLoader.Load<T>(dir)` `Assembly.LoadFrom`s each DLL in the plugins
dir (`<data dir>/plugins`, override `CLIPWELL_PLUGINS_DIR`) and instantiates concrete
`T`s. Plugins reference `Clipwell.Protocol` with `Private="false"` (single DLL, host
type identity). Built-ins ship in core; personal features → a private plugin. Example:
`plugins/sample`. See ADR-0006 + `engineering/.../plugins.mdx`.

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
ADR-0004. Per-OS global hotkey lives behind `IGlobalHotkey`; paste-into-source
behind `IPasteService`:
- **Windows** — `RegisterHotKey` (Alt+Shift+V) + `SendInput` Ctrl+V. Verified.
- **Linux** — `LinuxGlobalHotkey` (`XGrabKey` on the X11 root, Alt+Shift+V) +
  `LinuxPasteService` (`xdotool`/`wtype`). X11 only — no Wayland global grab.
- **macOS** — `MacGlobalHotkey` (Carbon `RegisterEventHotKey`, Option+Shift+V) +
  `MacPasteService` (CGEvent Cmd+V; needs Accessibility permission).

The mac/Linux paths are **compile-checked in CI** (the cross-platform `dotnet build`)
but their GUI behavior is **not yet verified on real hardware** — all interop is
guarded so a failure degrades to tray-only rather than crashing. The Avalonia
`TrayIcon` is cross-platform already (not OS-gated).

Picker position is centered by default; the `OpenAtCursor` setting opens it at the
mouse cursor (clamped to the screen) via `IPointerLocation` (Windows done) —
positioning runs inside the show-cycle, so keep it cheap.

## Documentation — HARD RULE (update docs with every feature)
Documentation is part of "done", not a follow-up. For **every** user-visible feature
or behavior change:
1. Update the **feature docs** (`docs/content/docs/`) — the affected scenario/page,
   and add the feature to the relevant list. Refresh `llms.txt` is automatic.
2. Add or refresh **media**: a screenshot (and a short usage clip where it helps) of
   the new behavior, under `docs/public/media/`. Re-capture screenshots whenever the
   UI changes so they never go stale.
   - **Use the capture scripts** — don't hand-roll: `pwsh bench/capture-shots.ps1`
     (picker + settings PNGs) and `pwsh bench/capture-clip.ps1` (a "filter as you
     type" WebM). Run them with **Windows PowerShell** (`powershell.exe`), not pwsh 7
     — they need `System.Drawing`/`System.Windows.Forms`.
   - **Both themes, always.** The docs sites are light/dark with the OS preference as
     default, so every screenshot/clip ships a `-light` and a `-dark` variant; embed
     via `<ThemedShot>` / `<ThemedClip>` so they swap with the docs theme. The scripts
     force the app theme with `CLIPWELL_THEME=light|dark`.
   - **No leaks.** The daemon watches the *global* clipboard, so the scripts seed an
     isolated DB, then **restart the daemon with `CLIPWELL_NO_WATCH=1`** (watcher off)
     before capturing — otherwise the user's live copies leak into the media. They also
     `PrintWindow` with `SetProcessDPIAware` so high-DPI windows aren't clipped.
   - Screenshot-only app hooks: `CLIPWELL_SHOW_SETTINGS=1` opens Settings on launch;
     `CLIPWELL_NO_AUTOHIDE=1` keeps the picker visible; **`CLIPWELL_CAPTURE=1`** shows
     the window off-screen and non-activating so capture **never steals focus** or
     pops a window over the user's work (PrintWindow still captures it). The
     capture-shots script sets this.
   - **Don't steal focus while the user is working.** `capture-shots.ps1` is
     focus-safe (above). But `capture-clip.ps1` (drives keystrokes) and
     `run-bench.ps1` (measures a real hotkey→show→activate cycle) *do* take focus —
     only run those when the user is idle, or ask first. Don't auto-relaunch the
     app in the foreground during dev.
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

## CI (cross-platform)
`.github/workflows/ci.yml` builds `clipwell.slnx` and runs a daemon smoke test on
**ubuntu / macOS / windows** runners. The smoke test (`scripts/ci-smoke.sh` for
Unix, `scripts/ci-smoke.ps1` for Windows) starts the built daemon against an
isolated DB, asserts `/health` + `/api/clipboard`, then does a real clipboard
round-trip through the native CLI to exercise the OS watcher. Linux runs under
`xvfb` with `xclip` installed. This is how the Unix watcher gets real coverage —
the GUI bits (tray, global hotkey) are not yet cross-platform and aren't covered.

## Documentation sites
Both sites are **light/dark themed with the OS preference as the default** (set on
Fumadocs' `RootProvider` in each `src/components/provider.tsx`). Theme-aware media
uses `<ThemedShot>`/`<ThemedClip>` (in `docs/src/components/`), which swap the
`-light`/`-dark` asset via the `.dark` class. `<video>` URLs need the Pages subpath,
exposed to the client as `NEXT_PUBLIC_PAGES_BASE` (set in `next.config.mjs`).
- `docs/` — user-facing feature docs (Fumadocs, Next.js static export). Landing page
  at `src/app/(home)/page.tsx`; content in `content/docs/`.
- `engineering/` — how-it-was-built site (same stack). Mermaid diagrams via the
  client `Mermaid` component; ADRs are a content section.
- Both deploy via `.github/workflows/docs.yml` to GitHub Pages: feature site at
  `/clipwell`, engineering at `/clipwell/engineering`. `PAGES_BASE` sets the subpath
  (empty for local builds). Build locally with `npm run build` in each.
