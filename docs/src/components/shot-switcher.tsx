'use client';
import { useState } from 'react';

/**
 * A picker screenshot the reader can flip between **surfaces** — the native
 * Avalonia UI on Windows / macOS / Linux and the web UI — to spot drift by eye.
 *
 * Media lives at `/media/shots/<name>/<surface>-<theme>.png`. Each surface pair
 * is theme-swapped with the docs `.dark` class (no JS), exactly like <ThemedShot>;
 * the surface is chosen with the tab row (local state).
 *
 * Pixel-diffing native against web is intentionally NOT done (different renderers
 * and fonts) — this component is the *human* parity review. See CLAUDE.md.
 *
 * Surfaces default to those currently captured. macOS/Linux native shots are
 * produced by the screenshots CI workflow; add 'macos'/'linux' to DEFAULT_SURFACES
 * (or pass `surfaces`) once their media is committed.
 */
const BASE = process.env.NEXT_PUBLIC_PAGES_BASE ?? '';

export type Surface = 'windows' | 'macos' | 'linux' | 'web';

const LABELS: Record<Surface, string> = {
  windows: 'Windows',
  macos: 'macOS',
  linux: 'Linux',
  web: 'Web',
};

// Canonical ordering; only surfaces with committed media should be listed here.
// (macOS native pending — CI screen-recording permission blocks its capture.)
export const DEFAULT_SURFACES: Surface[] = ['windows', 'linux', 'web'];

export function ShotSwitcher({
  name,
  alt,
  caption,
  surfaces = DEFAULT_SURFACES,
}: {
  name: string;
  alt: string;
  caption?: string;
  surfaces?: Surface[];
}) {
  const [active, setActive] = useState<Surface>(surfaces[0]);
  const common = 'rounded-b-xl rounded-tr-xl border border-fd-border shadow-lg w-full h-auto';
  const src = (surface: Surface, theme: 'light' | 'dark') =>
    `${BASE}/media/shots/${name}/${surface}-${theme}.png`;

  return (
    <figure className="my-6">
      <div role="tablist" aria-label={`${alt} — surface`} className="flex gap-1">
        {surfaces.map((s) => {
          const selected = s === active;
          return (
            <button
              key={s}
              type="button"
              role="tab"
              aria-selected={selected}
              onClick={() => setActive(s)}
              className={`rounded-t-md border border-b-0 px-3 py-1.5 text-sm font-medium transition-colors ${
                selected
                  ? 'border-fd-border bg-fd-card text-fd-foreground'
                  : 'border-transparent text-fd-muted-foreground hover:text-fd-foreground'
              }`}
            >
              {LABELS[s]}
            </button>
          );
        })}
      </div>
      {/* Render the active surface's light + dark variants; CSS picks by theme. */}
      <img
        src={src(active, 'light')}
        alt={`${alt} (${LABELS[active]})`}
        className={`${common} block dark:hidden`}
        loading="lazy"
      />
      <img
        src={src(active, 'dark')}
        alt={`${alt} (${LABELS[active]})`}
        className={`${common} hidden dark:block`}
        loading="lazy"
      />
      {caption ? (
        <figcaption className="mt-2 text-center text-sm text-fd-muted-foreground">
          {caption}
        </figcaption>
      ) : null}
    </figure>
  );
}
