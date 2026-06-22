import Image from 'next/image';
import { MEDIA_BASE } from '@/lib/media';

/**
 * A screenshot that swaps with the docs theme: the `-light` variant shows in
 * light mode, the `-dark` variant in dark mode. Pass the base name (without the
 * `-light`/`-dark` suffix or extension), e.g. `name="picker"` →
 * `<MEDIA_BASE>/picker-light.png` and `<MEDIA_BASE>/picker-dark.png`.
 *
 * Media is hosted on `media.aylith.com` (not in this repo) — see `@/lib/media`.
 * Both variants must share the same intrinsic dimensions so layout is stable
 * (no CLS) across the theme switch. `images.unoptimized` (static export) lets
 * next/image use the absolute remote URL directly.
 */
export function ThemedShot({
  name,
  alt,
  width,
  height,
  caption,
}: {
  name: string;
  alt: string;
  width: number;
  height: number;
  caption?: string;
}) {
  const common = 'rounded-xl border border-fd-border shadow-lg w-full h-auto';
  return (
    <figure className="my-6">
      <Image
        src={`${MEDIA_BASE}/${name}-light.png`}
        alt={alt}
        width={width}
        height={height}
        className={`${common} block dark:hidden`}
        priority={false}
      />
      <Image
        src={`${MEDIA_BASE}/${name}-dark.png`}
        alt={alt}
        width={width}
        height={height}
        className={`${common} hidden dark:block`}
        priority={false}
      />
      {caption ? (
        <figcaption className="mt-2 text-center text-sm text-fd-muted-foreground">
          {caption}
        </figcaption>
      ) : null}
    </figure>
  );
}
