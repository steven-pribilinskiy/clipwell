---
name: Clipwell
tagline: 'Your clipboard history, queryable by anything'
description: >-
  A fast, cross-platform clipboard-history service built as a headless daemon
  any app can query, listen to, and drive — with native and web pickers, a
  global hotkey, MCP, and a plugin system.
category: developer-tools
status: building
features:
  - 'Typed history (links, code, colors, paths, images) with instant search'
  - 'Single-digit-millisecond picker on a global hotkey, native and web'
  - >-
    Public API — REST, WebSocket/SSE, and MCP — so anything can read your
    clipboard
  - 'Pin, mask sensitive items, rename, group by date or source'
  - >-
    Local-first on Windows, macOS, and Linux — your history never leaves your
    machine
  - Extensible with plugins for custom item types and content-aware actions
targetUser: >-
  Developers and power users who copy-paste all day and want their clipboard to
  be searchable, scriptable, and AI-accessible
featured: false
order: 26
icon: >-
  M9 12h3.75M9 15h3.75M9 18h3.75m3 .75H18a2.25 2.25 0
  002.25-2.25V6.108c0-1.135-.845-2.098-1.976-2.192a48.424 48.424 0
  00-1.123-.08m-5.801 0c-.065.21-.1.433-.1.664 0 .414.336.75.75.75h4.5a.75.75 0
  00.75-.75 2.25 2.25 0 00-.1-.664m-5.8 0A2.251 2.251 0 0113.5 2.25H15c1.012 0
  1.867.668 2.15 1.586m-5.8 0c-.376.023-.75.05-1.124.08C9.095 4.01 8.25 4.973
  8.25 6.108V8.25m0 0H4.875c-.621 0-1.125.504-1.125 1.125v11.25c0 .621.504 1.125
  1.125 1.125h9.75c.621 0 1.125-.504
  1.125-1.125V9.375c0-.621-.504-1.125-1.125-1.125H8.25z
gradientFrom: '#7c3aed'
gradientTo: '#a78bfa'
---

## Vision

The clipboard is the most-used feature in computing and the least respected — a single slot that forgets everything the moment you copy again. Clipwell treats clipboard history as a first-class, queryable service: a headless daemon that watches, types, and stores everything you copy, then exposes it over a real API so the picker, the CLI, editor extensions, and AI agents are all just clients of the same contract. Your clipboard stops being a black box and becomes something you can search, script, and build on.

## The Problem

Clipboard managers are everywhere, but they're dead ends — closed apps that hoard your history behind a proprietary UI with no way to query it, automate it, or let an agent use it. They're also single-platform and single-purpose. Meanwhile the data is genuinely useful: the link you copied an hour ago, the snippet you reuse daily, the token you pasted into three terminals. None of it is reachable by the tools that could act on it.

## Key Differentiators

- **Queryable by anything**: REST for one-shot calls, WebSocket/SSE to live-stream changes, and MCP so AI agents can read and act on your history.
- **Two pickers, one daemon**: a native Avalonia picker and a Solid + Tauri web UI (also openable in any browser), both thin clients of the same API.
- **Fast by design**: the picker is pre-warmed and shows in a single frame on the global hotkey.
- **Local-first & private**: history lives in a local database on your machine; sensitive items are masked and never leave it.
- **Extensible**: typed detectors and content-aware actions load as plugins, so the set of item kinds and what you can do with them keeps growing.
