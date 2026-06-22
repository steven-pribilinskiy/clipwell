# Changelog

All notable changes to **Clipwell** — a cross-platform clipboard-history product
(headless daemon + picker / web UI / CLI / MCP, one public API). See the
[feature docs](https://aylith-labs.github.io/clipwell) for the full guide.

Screenshots and clips below adapt to your theme (light/dark) automatically; they
are hosted on [`media.aylith.com`](https://media.aylith.com/clipwell/media/),
not committed to this repo. The format follows
[Keep a Changelog](https://keepachangelog.com/). Dates are milestone dates —
Clipwell has not cut tagged releases yet.

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="https://media.aylith.com/clipwell/media/picker-dark.png">
  <img alt="The Clipwell picker showing a search box, filter tabs, and a list of typed clipboard items" width="430" src="https://media.aylith.com/clipwell/media/picker-light.png">
</picture>

---

## [Unreleased]

Nothing pending — the most recent work is captured in the dated milestones below.

---

## 2026-06-17 — Web UI & desktop shell

### Added
- **Web UI** — a second front-end (Solid + Vite + Tailwind v4, Bun + TypeScript),
  full feature-parity with the native picker, served by the daemon at
  `127.0.0.1:8787/app` over loopback CORS.
- **Tauri 2 desktop shell** — the web UI as a frameless desktop app: global
  hotkey, tray icon, hide-on-blur, and `enigo` paste-back. Starts hidden and
  hides on blur so it never sits over your work. Verified on Windows (both themes).

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="https://media.aylith.com/clipwell/media/webui-dark.png">
  <img alt="The Clipwell web UI as a Tauri desktop app: search, filter tabs, typed rows with favicons, image thumbnail, and source apps" width="720" src="https://media.aylith.com/clipwell/media/webui-light.png">
</picture>

---

## 2026-06-15 — Picker richness, MCP-over-HTTP, plugins

### Added
- **Action palette (Ctrl+K)** — content-aware actions for the selected item
  (open in browser, copy domain, …) via `IClipAction`.

  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="https://media.aylith.com/clipwell/media/actions-dark.png">
    <img alt="The Ctrl+K action palette over a selected link, listing Open in browser, Copy to clipboard, and Copy domain" width="430" src="https://media.aylith.com/clipwell/media/actions-light.png">
  </picture>

- **Quick Look (Ctrl+Y)** — a fast, full-content preview without leaving the list.

  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="https://media.aylith.com/clipwell/media/quicklook-dark.png">
    <img alt="Quick Look overlay showing the selected item's full content over a dimmed picker" width="430" src="https://media.aylith.com/clipwell/media/quicklook-light.png">
  </picture>

- **Compact / Detail view modes** — Detail adds a live preview pane beside the list.

  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="https://media.aylith.com/clipwell/media/detail-dark.png">
    <img alt="Clipwell in Detail view: the history list on the left and a preview pane on the right showing the selected item's full content" width="600" src="https://media.aylith.com/clipwell/media/detail-light.png">
  </picture>

- **Group by date or source** — inline headers above each day's / app's items.

  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="https://media.aylith.com/clipwell/media/grouped-dark.png">
    <img alt="The picker grouped by source app, with a header above each app's items" width="430" src="https://media.aylith.com/clipwell/media/grouped-light.png">
  </picture>

- **Richer settings** — appearance, default view & grouping, row metadata toggles,
  retention, and picker position.

  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="https://media.aylith.com/clipwell/media/settings-dark.png">
    <img alt="The Clipwell settings window: appearance, default view, grouping, row metadata, retention, and picker position" width="380" src="https://media.aylith.com/clipwell/media/settings-light.png">
  </picture>

- **Diagnostics window** — daemon health and real picker show-cycle latencies.

  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="https://media.aylith.com/clipwell/media/diagnostics-dark.png">
    <img alt="The Clipwell diagnostics window showing daemon status and picker show-cycle timings" width="430" src="https://media.aylith.com/clipwell/media/diagnostics-light.png">
  </picture>

- **Rebindable global hotkey chord**, **pagination / infinite scroll** (load older
  pages on scroll), and **source-app capture** (records the foreground app at copy
  time, shown per row).
- **Plugin host** — load external detectors (daemon) and actions (picker) from a
  plugins dir via `PluginLoader`; built-ins still ship in core.
- **MCP over HTTP/SSE** — the same four clipboard tools as the stdio server, now
  served in-process by the daemon (`AddMcpServer().WithHttpTransport()`).

### Changed
- Picker filtering is live as you type — narrowing the history without leaving the box:

  <video controls muted loop width="430" poster="https://media.aylith.com/clipwell/media/picker-dark.png">
    <source src="https://media.aylith.com/clipwell/media/usage-dark.webm" type="video/webm">
  </video>

  _(Light-theme clip: [usage-light.webm](https://media.aylith.com/clipwell/media/usage-light.webm).)_

---

## 2026-06-14 — Foundations

### Added
- **Clipboard daemon** — ASP.NET minimal API (REST + WebSocket/SSE + MCP) over a
  SQLite history store; one public API for every client.
- **Cross-platform capture** — Windows (Win32 clipboard listener, text/HTML/image),
  macOS/Linux (polling `pbpaste`/`wl-paste`/`xclip`), with a null fallback.
- **Native picker (Avalonia)** — pre-warmed window shown on a global hotkey
  (~16 ms warm show-cycle), tray icon, single-instance, paste-into-source app,
  image thumbnails, typed items + detectors (url / github-pr / email / jira / color
  / path / code / image).
- **Pin / sensitive / alias metadata** and picker type filters.
- **Documentation sites** — user-facing feature docs and an engineering / ADR site
  (Fumadocs static export), both light/dark with the OS preference as default.

[Unreleased]: https://github.com/aylith-labs/clipwell/commits/main
