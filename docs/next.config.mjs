import { createMDX } from 'fumadocs-mdx/next';

const withMDX = createMDX();

// PAGES_BASE is set by the GitHub Pages workflow (e.g. "/clipwell"); empty locally.
const base = process.env.PAGES_BASE || '';

/** @type {import('next').NextConfig} */
const config = {
  output: 'export',
  reactStrictMode: true,
  basePath: base || undefined,
  images: { unoptimized: true },
  // Exposed to client components (e.g. <ThemedClip>) so raw asset URLs like
  // <video src> get the Pages subpath prefix that next/image applies automatically.
  env: { NEXT_PUBLIC_PAGES_BASE: base },
};

export default withMDX(config);
