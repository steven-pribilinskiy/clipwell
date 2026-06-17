import { createResource, Show } from "solid-js";
import { API_BASE, getHealth } from "../lib/client";

export function DiagnosticsModal(props: { itemCount: number; onClose: () => void }) {
  const [health] = createResource(getHealth);
  return (
    <div
      class="absolute inset-0 z-50 flex justify-center bg-black/60 pt-12"
      onClick={props.onClose}
    >
      <div
        class="h-fit w-[460px] rounded-xl bg-white p-6 shadow-2xl dark:bg-zinc-900"
        onClick={(e) => e.stopPropagation()}
      >
        <h2 class="mb-4 text-xl font-semibold">Diagnostics</h2>
        <div class="flex flex-col gap-3 font-mono text-sm">
          <div>
            <div class="font-sans font-medium">API base</div>
            <div class="opacity-80">{API_BASE}</div>
          </div>
          <div>
            <div class="font-sans font-medium">Daemon</div>
            <Show
              when={health()}
              fallback={<div class="opacity-80">Unreachable — is it running?</div>}
            >
              <div class="opacity-80">status: {health()?.status}</div>
              <div class="opacity-80 break-all">db: {health()?.db}</div>
              <div class="opacity-80">subscribers: {health()?.subscribers}</div>
            </Show>
          </div>
          <div>
            <div class="font-sans font-medium">Loaded items</div>
            <div class="opacity-80">{props.itemCount}</div>
          </div>
        </div>
        <div class="mt-6 flex justify-end">
          <button
            type="button"
            class="rounded-md bg-violet-600 px-4 py-2 text-white hover:bg-violet-500"
            onClick={props.onClose}
          >
            Close
          </button>
        </div>
      </div>
    </div>
  );
}
