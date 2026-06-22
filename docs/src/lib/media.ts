/**
 * Base URL for themed media (screenshots + usage clips).
 *
 * Media is NOT committed to this repo — it lives on the aylith umbrella media
 * host (`media.aylith.com`, served by `infra-hub/stacks/media`). `<ThemedShot>`
 * and `<ThemedClip>` build `${MEDIA_BASE}/<name>-{light,dark}.{png,webm}` from
 * this. Capture locally, then upload with `infra-hub/stacks/media/upload.sh`.
 *
 * Override with `NEXT_PUBLIC_MEDIA_BASE` if you ever fork the host.
 */
export const MEDIA_BASE =
  process.env.NEXT_PUBLIC_MEDIA_BASE ?? 'https://media.aylith.com/clipwell/media';
