# ADR-0004: Pre-warmed picker window + global hotkey

- Status: accepted
- Date: 2026-06-14
- Deciders: project owner

## Context

The picker must appear within single-digit milliseconds of the user pressing the
hotkey — that responsiveness is a core product promise carried over from the
original Windows app (which measured ~4ms fastest / ~13ms average). Creating an
Avalonia window on each hotkey press cannot hit that; window realization is tens to
hundreds of milliseconds (measured ~170ms cold here).

## Decision

- **Pre-warm the window.** The picker `MainWindow` is created once at startup and
  kept alive. The global hotkey calls `ShowPicker()` which only `Show()`s and
  focuses the already-realized window. Escape / copy / focus-loss `Hide()` it
  instead of closing, so it stays warm.
- **Run as a background app.** `ShutdownMode = OnExplicitShutdown`; a tray icon
  provides Show / Quit. Closing the picker never quits the process.
- **Global hotkey behind an interface.** `IGlobalHotkey` with a Windows
  implementation (`RegisterHotKey` on a message-pump thread; default Alt+Shift+V,
  since Win+V is reserved by the OS). macOS/Linux implementations are a later phase.
- **Single instance** via a named mutex — the running instance owns the tray and
  hotkey.
- **Instrument every show.** `PerfLog` appends the show-cycle duration to
  `perf.log`, so the engineering docs can chart real latency.

## Consequences

- Measured: cold first show ~170ms (one-time window realization), warm shows
  **~6ms and below** — within the single-digit-ms bar.
- Hide-on-blur is the default; an env flag (`CLIPWELL_NO_AUTOHIDE`) disables it for
  automated screenshot tests.
- Still to do: paste-into-the-previously-focused-app after selection, open-at-cursor
  positioning, typed-item rendering (favicons/icons), pinned/sensitive filters,
  settings UI, and macOS/Linux hotkey + tray parity.
