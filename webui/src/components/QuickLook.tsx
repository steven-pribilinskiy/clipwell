import { Show } from "solid-js";
import { imageUrl } from "../lib/client";
import { fullText, meta } from "../lib/format";
import type { ClipItem } from "../types";

export function QuickLook(props: {
  item: ClipItem;
  showSource: boolean;
  showTime: boolean;
  onClose: () => void;
}) {
  const showImage = () => props.item.hasImage && !props.item.isSensitive;
  return (
    <div class="absolute inset-0 z-30 flex bg-black/75 p-9" onClick={props.onClose}>
      <div
        class="flex w-full flex-col rounded-xl bg-white p-5 shadow-2xl dark:bg-zinc-900"
        onClick={(e) => e.stopPropagation()}
      >
        <div class="mb-3">
          <div class="text-sm font-semibold">{props.item.kind ?? "text"}</div>
          <div class="text-[11px] opacity-60">
            {meta(props.item, props.showSource, props.showTime)}
          </div>
        </div>
        <div class="min-h-0 flex-1 overflow-auto">
          <Show
            when={showImage()}
            fallback={
              <pre class="whitespace-pre-wrap break-words font-mono text-sm">
                {fullText(props.item)}
              </pre>
            }
          >
            <img src={imageUrl(props.item.timestamp)} alt="" class="max-h-full" />
          </Show>
        </div>
      </div>
    </div>
  );
}
