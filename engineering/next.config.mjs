import { createMDX } from 'fumadocs-mdx/next';

const withMDX = createMDX();

// PAGES_BASE is set by the GitHub Pages workflow (e.g. "/clipwell/engineering").
const base = process.env.PAGES_BASE || '';

/** @type {import('next').NextConfig} */
const config = {
  output: 'export',
  reactStrictMode: true,
  basePath: base || undefined,
  images: { unoptimized: true },
};

export default withMDX(config);
