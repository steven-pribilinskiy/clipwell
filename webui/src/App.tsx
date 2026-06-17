import { createSignal, For, onCleanup, onMount, Show } from "solid-js";
import { ActionPalette } from "./components/ActionPalette";
import { ClipRow } from "./components/ClipRow";
import { DiagnosticsModal } from "./components/DiagnosticsModal";
import { QuickLook } from "./components/QuickLook";
import { SettingsModal } from "./components/SettingsModal";
import { imageUrl } from "./lib/client";
import { fullText, meta } from "./lib/format";
import { hide as platformHide } from "./lib/platform";
import { store } from "./store";
import { KIND_OPTIONS } from "./types";

export function App() {
  const [settingsOpen, setSettingsOpen] = createSignal(false);
  const [diagOpen, setDiagOpen] = createSignal(false);
  const [menuOpen, setMenuOpen] = createSignal(false);
  const [renameDraft, setRenameDraft] = createSignal("");

  let searchEl: HTMLInputElement | undefined;
  const overlayOpen = () =>
    settingsOpen() ||
    diagOpen() ||
    store.actionsOpen() ||
    store.quickLook() ||
    store.renameTs() !== null;

  onMount(() => {
    void store.init();
    window.addEventListener("keydown", onKey);
  });
  onCleanup(() => window.removeEventListener("keydown", onKey));

  function closeAll() {
    setSettingsOpen(false);
    setDiagOpen(false);
    setMenuOpen(false);
    store.setActionsOpen(false);
    store.setQuickLook(false);
    store.setRenameTs(null);
  }

  function openActions() {
    if (store.selected()) store.setActionsOpen(true);
  }
  function beginRename() {
    store.beginRename();
    setRenameDraft(store.selected()?.alias ?? "");
  }

  function onKey(e: KeyboardEvent) {
    const typing = ["INPUT", "TEXTAREA", "SELECT"].includes(document.activeElement?.tagName ?? "");
    if (e.key === "Escape") {
      if (overlayOpen()) {
        closeAll();
      } else {
        void platformHide();
      }
      return;
    }
    // Action palette / rename own the keyboard while open.
    if (store.actionsOpen() || store.renameTs() !== null || settingsOpen() || diagOpen()) return;

    const ctrl = e.ctrlKey || e.metaKey;
    if (e.key === "ArrowDown") {
      e.preventDefault();
      store.moveSelection(1);
    } else if (e.key === "ArrowUp") {
      e.preventDefault();
      store.moveSelection(-1);
    } else if (e.key === "Enter") {
      e.preventDefault();
      void store.chooseSelected();
    } else if (e.key === "Delete" && !typing) {
      void store.del();
    } else if (ctrl && e.key.toLowerCase() === "p") {
      e.preventDefault();
      void store.togglePin();
    } else if (ctrl && e.key.toLowerCase() === "e") {
      e.preventDefault();
      void store.toggleSensitive();
    } else if (ctrl && e.key.toLowerCase() === "y") {
      e.preventDefault();
      if (store.selected()) store.setQuickLook(true);
    } else if (ctrl && e.key.toLowerCase() === "k") {
      e.preventDefault();
      openActions();
    } else if (ctrl && e.key === "1") {
      store.setTab("all");
    } else if (ctrl && e.key === "2") {
      store.setTab("pinned");
    } else if (ctrl && e.key === "3") {
      store.setTab("sensitive");
    } else if (e.key === "F2") {
      e.preventDefault();
      beginRename();
    }
  }

  function onScroll(e: Event) {
    const el = e.currentTarget as HTMLElement;
    if (el.scrollHeight - el.scrollTop - el.clientHeight < 400) void store.loadMore();
  }

  const tabBtn = (active: boolean) =>
    `rounded-md px-3 py-1 text-sm ${active ? "bg-violet-600 text-white" : "bg-zinc-200 dark:bg-zinc-800"}`;
  const ctl =
    "rounded-md border border-zinc-300 bg-transparent px-2 py-1 text-sm dark:border-zinc-700";

  return (
    <div class="relative flex h-full flex-col gap-3 p-4">
      {/* Search */}
      <input
        ref={searchEl}
        autofocus
        value={store.search()}
        onInput={(e) => store.setSearch(e.currentTarget.value)}
        placeholder="Search clipboard history…"
        class="w-full rounded-lg border border-zinc-300 bg-white px-3 py-2 text-base outline-none focus:border-violet-500 dark:border-zinc-700 dark:bg-zinc-900"
      />

      {/* Filter bar */}
      <div class="flex flex-wrap items-center gap-2">
        <button
          type="button"
          class={tabBtn(store.tab() === "all")}
          onClick={() => store.setTab("all")}
        >
          All
        </button>
        <button
          type="button"
          class={tabBtn(store.tab() === "pinned")}
          onClick={() => store.setTab("pinned")}
          title="Pinned"
        >
          📌
        </button>
        <button
          type="button"
          class={tabBtn(store.tab() === "sensitive")}
          onClick={() => store.setTab("sensitive")}
          title="Sensitive"
        >
          🔒
        </button>
        <button
          type="button"
          class={ctl}
          onClick={() => store.setView(store.view() === "detail" ? "compact" : "detail")}
          title="Compact / Detail"
        >
          {store.view() === "detail" ? "▤" : "▦"}
        </button>
        <div class="flex-1" />
        <select
          class={ctl}
          value={store.group()}
          onChange={(e) => store.setGroup(e.currentTarget.value as "none" | "date" | "source")}
        >
          <option value="none">No grouping</option>
          <option value="date">By date</option>
          <option value="source">By source</option>
        </select>
        <select
          class={ctl}
          value={store.kind()}
          onChange={(e) => store.setKind(e.currentTarget.value)}
        >
          <For each={KIND_OPTIONS}>{(k) => <option value={k.value}>{k.label}</option>}</For>
        </select>
        <div class="relative">
          <button type="button" class={ctl} onClick={() => setMenuOpen(!menuOpen())}>
            ⚙
          </button>
          <Show when={menuOpen()}>
            <div class="absolute right-0 z-20 mt-1 w-40 rounded-md border border-zinc-200 bg-white py-1 shadow-lg dark:border-zinc-700 dark:bg-zinc-900">
              <button
                type="button"
                class="block w-full px-3 py-1.5 text-left text-sm hover:bg-zinc-100 dark:hover:bg-zinc-800"
                onClick={() => {
                  setMenuOpen(false);
                  setSettingsOpen(true);
                }}
              >
                Settings…
              </button>
              <button
                type="button"
                class="block w-full px-3 py-1.5 text-left text-sm hover:bg-zinc-100 dark:hover:bg-zinc-800"
                onClick={() => {
                  setMenuOpen(false);
                  setDiagOpen(true);
                }}
              >
                Diagnostics…
              </button>
              <button
                type="button"
                class="block w-full px-3 py-1.5 text-left text-sm text-red-600 hover:bg-zinc-100 dark:hover:bg-zinc-800"
                onClick={() => {
                  setMenuOpen(false);
                  if (confirm("Delete ALL clipboard history?")) void store.clearAllHistory();
                }}
              >
                Clear history…
              </button>
            </div>
          </Show>
        </div>
      </div>

      {/* List + optional detail pane */}
      <div class="flex min-h-0 flex-1 gap-3">
        <div class="relative min-w-0 flex-1">
          {/* Rename bar */}
          <Show when={store.renameTs()}>
            <div class="absolute inset-x-0 top-0 z-10 flex items-center gap-2 rounded-lg border border-violet-500 bg-white p-2 dark:bg-zinc-900">
              <span class="text-sm opacity-70">Rename:</span>
              <input
                autofocus
                class="flex-1 rounded-md border border-zinc-300 bg-transparent px-2 py-1 outline-none dark:border-zinc-700"
                value={renameDraft()}
                onInput={(e) => setRenameDraft(e.currentTarget.value)}
                onKeyDown={(e) => {
                  if (e.key === "Enter") {
                    e.preventDefault();
                    void store.commitRename(renameDraft());
                    searchEl?.focus();
                  } else if (e.key === "Escape") {
                    e.preventDefault();
                    store.setRenameTs(null);
                    searchEl?.focus();
                  }
                }}
                placeholder="alias (empty to clear)"
              />
            </div>
          </Show>
          <div class="h-full overflow-auto" onScroll={onScroll}>
            <For each={store.rows()} fallback={<div class="p-4 opacity-50">{store.status()}</div>}>
              {(row) => (
                <ClipRow
                  row={row}
                  selected={row.item.timestamp === store.selectedTs()}
                  showSource={store.settings().showSource}
                  showTime={store.settings().showTime}
                  onSelect={() => store.select(row.item.timestamp)}
                  onChoose={() => void store.chooseSelected()}
                />
              )}
            </For>
          </div>
        </div>

        <Show when={store.view() === "detail" && store.selected()}>
          <div class="w-[340px] shrink-0 overflow-auto rounded-lg bg-zinc-200/50 p-4 dark:bg-zinc-900/60">
            {(() => {
              const item = store.selected()!;
              const showImage = item.hasImage && !item.isSensitive;
              return (
                <>
                  <div class="text-sm font-semibold">{item.kind ?? "text"}</div>
                  <div class="mb-3 text-[11px] opacity-60">
                    {meta(item, store.settings().showSource, store.settings().showTime)}
                  </div>
                  <Show
                    when={showImage}
                    fallback={
                      <pre class="whitespace-pre-wrap break-words font-mono text-[12.5px]">
                        {fullText(item)}
                      </pre>
                    }
                  >
                    <img src={imageUrl(item.timestamp)} alt="" class="max-w-full" />
                  </Show>
                </>
              );
            })()}
          </div>
        </Show>
      </div>

      {/* Status */}
      <div class="text-[11px] opacity-50">{store.status()}</div>

      {/* Overlays */}
      <Show when={store.quickLook() && store.selected()}>
        <QuickLook
          item={store.selected()!}
          showSource={store.settings().showSource}
          showTime={store.settings().showTime}
          onClose={() => store.setQuickLook(false)}
        />
      </Show>
      <Show when={store.actionsOpen() && store.selected()}>
        <ActionPalette item={store.selected()!} onClose={() => store.setActionsOpen(false)} />
      </Show>
      <Show when={settingsOpen()}>
        <SettingsModal
          settings={store.settings()}
          onClose={() => setSettingsOpen(false)}
          onSave={(s) => {
            void store.saveSettings(s);
            setSettingsOpen(false);
          }}
        />
      </Show>
      <Show when={diagOpen()}>
        <DiagnosticsModal itemCount={store.items().length} onClose={() => setDiagOpen(false)} />
      </Show>
    </div>
  );
}
