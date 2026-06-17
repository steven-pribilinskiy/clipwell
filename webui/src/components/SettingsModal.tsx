import { createStore } from "solid-js/store";
import type { ClipboardSettings } from "../types";

const RETENTIONS: { label: string; value: number | null }[] = [
  { label: "7 days", value: 7 },
  { label: "30 days", value: 30 },
  { label: "90 days", value: 90 },
  { label: "Forever", value: null },
];

const field =
  "rounded-md border border-zinc-300 bg-transparent px-3 py-2 outline-none focus:border-violet-500 dark:border-zinc-700";

export function SettingsModal(props: {
  settings: ClipboardSettings;
  onSave: (s: ClipboardSettings) => void;
  onClose: () => void;
}) {
  const [draft, setDraft] = createStore<ClipboardSettings>({ ...props.settings });

  return (
    <div
      class="absolute inset-0 z-50 flex justify-center bg-black/60 pt-12"
      onClick={props.onClose}
    >
      <div
        class="h-fit max-h-[90%] w-[460px] overflow-auto rounded-xl bg-white p-6 shadow-2xl dark:bg-zinc-900"
        onClick={(e) => e.stopPropagation()}
      >
        <h2 class="mb-4 text-xl font-semibold">Settings</h2>
        <div class="flex flex-col gap-4">
          <label class="flex flex-col gap-1">
            <span class="font-medium">Appearance</span>
            <select
              class={field}
              value={draft.theme}
              onChange={(e) =>
                setDraft("theme", e.currentTarget.value as ClipboardSettings["theme"])
              }
            >
              <option value="system">System</option>
              <option value="light">Light</option>
              <option value="dark">Dark</option>
            </select>
          </label>
          <label class="flex flex-col gap-1">
            <span class="font-medium">Default view</span>
            <select
              class={field}
              value={draft.defaultView}
              onChange={(e) =>
                setDraft("defaultView", e.currentTarget.value as ClipboardSettings["defaultView"])
              }
            >
              <option value="compact">Compact</option>
              <option value="detail">Detail</option>
            </select>
          </label>
          <label class="flex flex-col gap-1">
            <span class="font-medium">Default grouping</span>
            <select
              class={field}
              value={draft.defaultGroup}
              onChange={(e) =>
                setDraft("defaultGroup", e.currentTarget.value as ClipboardSettings["defaultGroup"])
              }
            >
              <option value="none">No grouping</option>
              <option value="date">By date</option>
              <option value="source">By source</option>
            </select>
          </label>
          <label class="flex items-center gap-2">
            <input
              type="checkbox"
              checked={draft.showSource}
              onChange={(e) => setDraft("showSource", e.currentTarget.checked)}
            />
            <span>Show source app</span>
          </label>
          <label class="flex items-center gap-2">
            <input
              type="checkbox"
              checked={draft.showTime}
              onChange={(e) => setDraft("showTime", e.currentTarget.checked)}
            />
            <span>Show time</span>
          </label>
          <label class="flex flex-col gap-1">
            <span class="font-medium">History retention</span>
            <select
              class={field}
              value={String(draft.retentionDays)}
              onChange={(e) => {
                const v = e.currentTarget.value;
                setDraft("retentionDays", v === "null" ? null : Number(v));
              }}
            >
              {RETENTIONS.map((r) => (
                <option value={String(r.value)}>{r.label}</option>
              ))}
            </select>
          </label>
          <label class="flex items-center gap-2">
            <input
              type="checkbox"
              checked={draft.openAtCursor}
              onChange={(e) => setDraft("openAtCursor", e.currentTarget.checked)}
            />
            <span>Open picker at the mouse cursor (desktop)</span>
          </label>
          <label class="flex flex-col gap-1">
            <span class="font-medium">Global hotkey (desktop)</span>
            <input
              class={field}
              value={draft.hotkey}
              onInput={(e) => setDraft("hotkey", e.currentTarget.value)}
              placeholder="Alt+Shift+V"
            />
          </label>
        </div>
        <div class="mt-6 flex justify-end gap-2">
          <button
            type="button"
            class="rounded-md px-4 py-2 hover:bg-zinc-100 dark:hover:bg-zinc-800"
            onClick={props.onClose}
          >
            Close
          </button>
          <button
            type="button"
            class="rounded-md bg-violet-600 px-4 py-2 text-white hover:bg-violet-500"
            onClick={() => props.onSave({ ...draft })}
          >
            Save
          </button>
        </div>
      </div>
    </div>
  );
}
