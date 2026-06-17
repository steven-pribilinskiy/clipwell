import { createSignal, Show } from "solid-js";
import { imageUrl } from "../lib/client";
import { faviconUrl, kindGlyph, meta, preview } from "../lib/format";
import type { Row } from "../store";

export function ClipRow(props: {
  row: Row;
  selected: boolean;
  showSource: boolean;
  showTime: boolean;
  onSelect: () => void;
  onChoose: () => void;
}) {
  const [iconFailed, setIconFailed] = createSignal(false);
  const item = () => props.row.item;
  const thumb = () => item().hasImage && !item().isSensitive;
  const favicon = () => (item().isSensitive ? null : faviconUrl(item()));
  const showImageIcon = () => thumb() || (favicon() && !iconFailed());

  return (
    <>
      <Show when={props.row.header}>
        <div class="px-2 pt-3 pb-1 text-[11px] font-semibold uppercase tracking-wide opacity-50">
          {props.row.header}
        </div>
      </Show>
      <button
        type="button"
        onClick={props.onSelect}
        onDblClick={props.onChoose}
        class={`flex w-full items-start gap-3 rounded-lg px-3 py-2 text-left transition-colors ${
          props.selected ? "bg-violet-600 text-white" : "hover:bg-zinc-200 dark:hover:bg-zinc-800"
        }`}
        title={item().isSensitive ? "" : (item().textContent ?? "")}
      >
        <span class="mt-0.5 flex h-6 w-6 shrink-0 items-center justify-center">
          <Show
            when={showImageIcon()}
            fallback={<span class="text-[15px]">{kindGlyph(item().kind)}</span>}
          >
            <img
              src={thumb() ? imageUrl(item().timestamp) : (favicon() as string)}
              alt=""
              class="h-6 w-6 rounded object-cover"
              onError={() => setIconFailed(true)}
            />
          </Show>
        </span>
        <span class="min-w-0 flex-1">
          <span class="line-clamp-2 break-words text-sm">{preview(item())}</span>
          <span class={`block text-[11px] ${props.selected ? "text-violet-200" : "opacity-55"}`}>
            {meta(item(), props.showSource, props.showTime)}
          </span>
        </span>
        <Show when={item().isUserPinned}>
          <span class="mt-0.5 text-[13px]">📌</span>
        </Show>
      </button>
    </>
  );
}
