# Clipwell

A fast, cross-platform clipboard-history service that any app can query, listen to, and drive.

Clipwell is built as a **headless daemon** with a thin UI on top. The daemon watches the
system clipboard, stores typed history in SQLite, and exposes everything over a public API —
**REST**, **WebSocket/SSE**, and **MCP** — so the picker UI, the CLI, editor extensions, and AI
agents are all just clients of the same contract. Nothing is hidden behind a private backdoor.

> Status: early development. The daemon (Phase 1) is the current focus; the cross-platform UI
> and the two documentation sites follow. See the plan and ADRs under `engineering/`.

## Why

- **Queryable by anything** — REST for one-shot calls, WebSocket/SSE to live-stream clipboard
  changes, MCP so Claude (and other agents) can read and act on your clipboard history.
- **Cross-platform** — Windows, macOS, and Linux behind one clipboard-watcher interface.
- **Fast** — the picker is pre-warmed and shows in single-digit milliseconds on the
  global hotkey (Alt+Shift+V on Windows). It runs in the background with a tray icon.
- **Extensible** — typed clipboard items and content-aware actions load as plugins.

## Repository layout

```
daemon/        clipboard watcher + SQLite history + REST/WS(SSE)/MCP server (.NET 10)
protocol/      shared domain model + plugin contract (IClipDetector / IClipAction)
cli/           clipwell list/pin/clear — a reference API consumer
ui/            picker + settings (cross-platform; stack TBD)
mcp/           stdio MCP wrapper that proxies to the running daemon
docs/          user-facing feature documentation site
engineering/   how-it-was-built engineering site (architecture, ADRs, perf)
```

## Build

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```sh
dotnet build
dotnet run --project daemon      # clipboard daemon on http://127.0.0.1:8787
dotnet run --project ui          # the picker (connects to the daemon)
dotnet run --project cli -- list # reference CLI client
```

## API surface

The daemon exposes the same history three ways:

- **REST** — `GET /api/clipboard`, `/settings`, `/image/{ts}`, `POST /delete`, `/clear`.
  Machine-readable spec at `GET /openapi/v1.json` (also checked in at
  [`openapi/clipwell.v1.json`](./openapi/clipwell.v1.json)).
- **Live** — `GET /api/clipboard/stream` (SSE) and `/api/clipboard/ws` (WebSocket)
  push a `clipboard.changed` event on every capture.
- **MCP** — the `mcp/` project is a stdio MCP server exposing `clipboard_recent`,
  `clipboard_search`, `clipboard_get_text`, and `clipboard_clear`. Point an MCP
  client (Claude Desktop / Claude Code) at the built `Clipwell.Mcp` executable; it
  proxies to the running daemon (`CLIPWELL_API`, default `http://127.0.0.1:8787`).

## License

[MIT](./LICENSE)
