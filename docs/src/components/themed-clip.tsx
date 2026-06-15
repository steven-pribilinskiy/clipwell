'use client';
import { usePathname } from 'next/navigation';

/**
 * A short, muted, looping usage clip that swaps with the docs theme (same idea
 * as <ThemedShot>, for video). Pass the base name (without `-light`/`-dark` or
 * extension): `name="usage"` → `/media/usage-light.webm` + `/media/usage-dark.webm`.
 *
 * `next/image` would prefix the Pages basePath automatically; <video> does not,
 * so we read it from `next.config`'s injected basePath via the router pathname's
 * origin is not enough — instead we rely on Next's `assetPrefix`/`basePath` being
 * applied at build by referencing the public path with the configured prefix.
 */
const BASE = process.env.NEXT_PUBLIC_PAGES_BASE ?? '';

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
  const src = (theme: 'light' | 'dark') => `${BASE}/media/${name}-${theme}.webm`;
  const posterSrc = (theme: 'light' | 'dark') =>
    poster ? `${BASE}/media/${poster}-${theme}.png` : undefined;
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
