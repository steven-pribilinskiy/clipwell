import { createMemo, createSignal, For } from "solid-js";
import { actionsFor } from "../lib/actions";
import type { ClipItem } from "../types";

export function ActionPalette(props: { item: ClipItem; onClose: () => void }) {
  const [query, setQuery] = createSignal("");
  const [idx, setIdx] = createSignal(0);
  const all = actionsFor(props.item);
  const filtered = createMemo(() =>
    all.filter((a) => a.label.toLowerCase().includes(query().trim().toLowerCase())),
  );

  const run = async () => {
    const a = filtered()[idx()];
    props.onClose();
    if (a) await a.execute(props.item);
  };

  const onKey = (e: KeyboardEvent) => {
    if (e.key === "Enter") {
      e.preventDefault();
      void run();
    } else if (e.key === "Escape") {
      e.preventDefault();
      props.onClose();
    } else if (e.key === "ArrowDown") {
      e.preventDefault();
      setIdx((i) => Math.min(filtered().length - 1, i + 1));
    } else if (e.key === "ArrowUp") {
      e.preventDefault();
      setIdx((i) => Math.max(0, i - 1));
    }
  };

  return (
    <div
      class="absolute inset-0 z-40 flex justify-center bg-black/60 pt-20"
      onClick={props.onClose}
    >
      <div
        class="h-fit w-[420px] rounded-xl bg-white p-2 shadow-2xl dark:bg-zinc-900"
        onClick={(e) => e.stopPropagation()}
      >
        <input
          autofocus
          value={query()}
          onInput={(e) => {
            setQuery(e.currentTarget.value);
            setIdx(0);
          }}
          onKeyDown={onKey}
          placeholder="Actions…"
          class="mb-2 w-full rounded-md border border-zinc-300 bg-transparent px-3 py-2 outline-none focus:border-violet-500 dark:border-zinc-700"
        />
        <For each={filtered()} fallback={<div class="px-3 py-2 opacity-50">No actions</div>}>
          {(a, i) => (
            <button
              type="button"
              onMouseEnter={() => setIdx(i())}
              onClick={() => void run()}
              class={`block w-full rounded-md px-3 py-2 text-left ${
                i() === idx()
                  ? "bg-violet-600 text-white"
                  : "hover:bg-zinc-100 dark:hover:bg-zinc-800"
              }`}
            >
              {a.label}
            </button>
          )}
        </For>
      </div>
    </div>
  );
}
