// Bridges the few things that only the Tauri desktop shell can do. In a plain
// browser these degrade gracefully (no-ops), so the same SPA runs everywhere.

interface TauriInternals {
  invoke?: (cmd: string, args?: unknown) => Promise<unknown>;
}

function tauri(): TauriInternals | undefined {
  return (window as unknown as { __TAURI_INTERNALS__?: TauriInternals }).__TAURI_INTERNALS__;
}

function isTauri(): boolean {
  return tauri()?.invoke !== undefined;
}

async function invoke(cmd: string, args?: unknown): Promise<void> {
  try {
    await tauri()?.invoke?.(cmd, args);
  } catch {
    /* command missing or failed — ignore */
  }
}

/** Hide the picker and paste the clipboard into the previously focused app (Tauri only). */
export async function paste(): Promise<void> {
  if (isTauri()) await invoke("paste_and_hide");
}

/** Hide the picker window (Tauri only; in a browser this is a no-op). */
export async function hide(): Promise<void> {
  if (isTauri()) await invoke("hide_window");
}

/** Re-register the global hotkey from settings (Tauri only). */
export async function setHotkey(chord: string): Promise<void> {
  if (isTauri()) await invoke("set_hotkey", { chord });
}

/** Open a URL/path with the OS default handler (Tauri opener; browser falls back). */
export async function openExternal(target: string): Promise<void> {
  if (isTauri()) {
    await invoke("open_external", { target });
  } else {
    window.open(target, "_blank", "noopener");
  }
}
