import Image from 'next/image';

/**
 * A screenshot that swaps with the docs theme: the `-light` variant shows in
 * light mode, the `-dark` variant in dark mode. Pass the base name (without the
 * `-light`/`-dark` suffix or extension), e.g. `name="picker"` →
 * `/media/picker-light.png` and `/media/picker-dark.png`.
 *
 * Both variants must share the same intrinsic dimensions so layout is stable
 * (no CLS) across the theme switch. `next/image` prefixes the Pages basePath.
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
        src={`/media/${name}-light.png`}
        alt={alt}
        width={width}
        height={height}
        className={`${common} block dark:hidden`}
        priority={false}
      />
      <Image
        src={`/media/${name}-dark.png`}
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
