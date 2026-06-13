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
- **Fast** — the picker targets single-digit-millisecond visibility via a pre-warmed window.
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
dotnet run --project daemon
```

## License

[MIT](./LICENSE)
