'use client';
import { usePathname } from 'next/navigation';
import { MEDIA_BASE } from '@/lib/media';

/**
 * A short, muted, looping usage clip that swaps with the docs theme (same idea
 * as <ThemedShot>, for video). Pass the base name (without `-light`/`-dark` or
 * extension): `name="usage"` → `<MEDIA_BASE>/usage-light.webm` + `…-dark.webm`.
 *
 * Media is hosted on `media.aylith.com` (absolute URLs, not in this repo), so
 * unlike the old local `/media/` paths the <video> src needs no Pages-basePath
 * prefix — see `@/lib/media`.
 */

export function ThemedClip({
  name,
  poster,
  width,
  height,
  caption,
}: {
  name: string;
  poster?: string;
  width: number;
  height: number;
  caption?: string;
}) {
  // Touch usePathname so this stays a client component (video autoplay needs DOM).
  usePathname();
  const common =
    'rounded-xl border border-fd-border shadow-lg w-full h-auto';
  const src = (theme: 'light' | 'dark') => `${MEDIA_BASE}/${name}-${theme}.webm`;
  const posterSrc = (theme: 'light' | 'dark') =>
    poster ? `${MEDIA_BASE}/${poster}-${theme}.png` : undefined;
  return (
    <figure className="my-6">
      <video
        className={`${common} block dark:hidden`}
        width={width}
        height={height}
        poster={posterSrc('light')}
        autoPlay
        loop
        muted
        playsInline
      >
        <source src={src('light')} type="video/webm" />
      </video>
      <video
        className={`${common} hidden dark:block`}
        width={width}
        height={height}
        poster={posterSrc('dark')}
        autoPlay
        loop
        muted
        playsInline
      >
        <source src={src('dark')} type="video/webm" />
      </video>
      {caption ? (
        <figcaption className="mt-2 text-center text-sm text-fd-muted-foreground">
          {caption}
        </figcaption>
      ) : null}
    </figure>
  );
}
