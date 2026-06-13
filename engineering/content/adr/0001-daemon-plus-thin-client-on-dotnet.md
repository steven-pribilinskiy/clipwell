# ADR-0001: Headless daemon + thin client, on .NET 10

- Status: accepted
- Date: 2026-06-13
- Deciders: project owner

## Context

Clipwell began as a Windows-only WPF app that was a thin HTTP client of a backend living
inside a separate monorepo (windows-settings). The goal now is a standalone, cross-platform
(Windows/macOS/Linux) clipboard-history product that is **fully queryable by other apps** —
REST, WebSocket/SSE, and MCP — so the picker UI, a CLI, editor integrations, and AI agents are
all peers on one public contract.

Two structural questions had to be answered before any code:

1. **Process model** — one app that does everything, or a split?
2. **Implementation language for the daemon** — which keeps the most options open for the
   still-undecided UI stack while letting us reuse the proven domain logic?

## Decision

**Split architecture: a headless daemon plus a thin UI client.** The daemon owns the clipboard
watcher, the SQLite history, retention, and the public API surface. Every other component —
including the first-party picker UI — talks to the daemon over the *same* public API that
third parties use. The UI dogfoods the contract; there is no private channel.

**The daemon is written in C# on .NET 10.** Rationale:

- The leading UI candidate (Avalonia) is also .NET, so this keeps the picker + daemon +
  plugin contract in one language if Avalonia is chosen.
- If the UI later goes web-based (Tauri), nothing is lost — the daemon is a separate process
  reached over HTTP/WS regardless of UI language.
- The existing backend's SQLite schema and upsert/query/retention logic
  (`clipboard-store.ts`) port directly; the WPF app's detector/action *concepts* port as C#
  interfaces.
- ASP.NET minimal APIs (Kestrel) give REST + WebSocket in-process; the official
  `modelcontextprotocol/csharp-sdk` covers MCP.

## Consequences

- The old WPF picker can be repointed at the new daemon by changing one URL constant — it
  becomes the daemon's first integration test without any rewrite.
- Plugins (typed-item detectors, content-aware actions) load into the daemon as assemblies,
  which sets up the public/private split: generic detectors ship in the public repo; personal
  ones (Claude sessions, Jira, repo cache) ship as a private plugin.
- A two-language repo is possible if the UI ends up non-.NET; accepted as a known tradeoff.
- Running a background daemon means lifecycle concerns (autostart, single-instance, health)
  that a single-process app would not have. Deferred to a later ADR.
