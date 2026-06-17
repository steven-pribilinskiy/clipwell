import type { ClipItem } from "../types";
import { openExternal } from "./platform";

export interface ClipAction {
  id: string;
  label: string;
  appliesTo(item: ClipItem): boolean;
  execute(item: ClipItem): Promise<void>;
}

async function copy(text: string): Promise<void> {
  try {
    await navigator.clipboard.writeText(text);
  } catch {
    /* clipboard unavailable */
  }
}

// Built-in actions, mirroring ui/Actions/BuiltInActions.cs. (Plugin actions are .NET
// and load only in the Avalonia UI; a JS plugin mechanism for the web UI is future.)
const ACTIONS: ClipAction[] = [
  {
    id: "open-url",
    label: "Open in browser",
    appliesTo: (i) => i.kind === "url" || i.kind === "github-pr",
    execute: (i) => openExternal((i.textContent ?? "").trim()),
  },
  {
    id: "open-path",
    label: "Open file or folder",
    appliesTo: (i) => i.kind === "path",
    execute: (i) => openExternal((i.textContent ?? "").trim()),
  },
  {
    id: "copy",
    label: "Copy to clipboard",
    appliesTo: (i) => !!i.textContent && !i.isSensitive,
    execute: (i) => copy(i.textContent ?? ""),
  },
  {
    id: "copy-host",
    label: "Copy domain",
    appliesTo: (i) => i.kind === "url" || i.kind === "github-pr",
    execute: async (i) => {
      try {
        await copy(new URL((i.textContent ?? "").trim()).host);
      } catch {
        /* not a url */
      }
    },
  },
];

export function actionsFor(item: ClipItem): ClipAction[] {
  return ACTIONS.filter((a) => a.appliesTo(item));
}
