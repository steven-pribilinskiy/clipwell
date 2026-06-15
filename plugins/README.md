# Clipwell plugins

Clipwell is extensible through two contracts in `Clipwell.Protocol.Plugins`:

- **`IClipDetector`** — classify an item into a custom *kind*. Loaded by the
  **daemon** (classification happens at read time).
- **`IClipAction`** — a content-aware action shown in the **Ctrl+K** palette. Loaded
  by the **picker** (UI). Gets an `IClipActionContext` for side effects
  (`OpenUrl`, `OpenPath`, `SetClipboardAsync`, `Notify`) so it never depends on the UI.

A single plugin DLL can contain both. The daemon picks up the detectors; the picker
picks up the actions.

## Write a plugin

1. New class library targeting `net10.0`, referencing `Clipwell.Protocol` with
   **`Private="false"`** (do **not** copy `Clipwell.Protocol.dll` into your output —
   the host already has it loaded; a second copy breaks interface type identity).
2. Implement `IClipDetector` and/or `IClipAction` with a public parameterless ctor.
3. `dotnet build`, then drop **only your plugin's `.dll`** into the plugins folder.

See [`sample/`](sample) for a complete example (a `TODO` detector and a
"Copy as SHOUTING" action).

## Where plugins load from

`<data dir>/plugins` by default — i.e.:

| OS | Path |
|----|------|
| Windows | `%APPDATA%\Roaming\Clipwell\plugins` |
| Linux | `~/.config/Clipwell/plugins` |
| macOS | `~/Library/Application Support/Clipwell/plugins` |

Override the whole path with `CLIPWELL_PLUGINS_DIR`. (`CLIPWELL_DATA_DIR` moves the
default base.) A plugin that fails to load is skipped — never fatal.

> Personal/workflow plugins (Claude-session, Jira, terminal integrations, etc.) live
> in a **separate private repo** and load the same way; the public core ships only
> the contracts + this sample.
