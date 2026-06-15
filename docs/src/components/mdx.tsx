import defaultMdxComponents from 'fumadocs-ui/mdx';
import type { MDXComponents } from 'mdx/types';
import { ThemedShot } from '@/components/themed-shot';
import { ThemedClip } from '@/components/themed-clip';

export function getMDXComponents(components?: MDXComponents) {
  return {
    ...defaultMdxComponents,
    ThemedShot,
    ThemedClip,
    ...components,
  } satisfies MDXComponents;
}

export const useMDXComponents = getMDXComponents;

declare global {
  type MDXProvidedComponents = ReturnType<typeof getMDXComponents>;
}
