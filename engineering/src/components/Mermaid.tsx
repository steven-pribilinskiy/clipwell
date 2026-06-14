'use client';

import { useEffect, useId, useState } from 'react';

/**
 * Renders a Mermaid diagram client-side. Works with static export — mermaid is
 * imported lazily in the browser, never at build/SSR time.
 */
export function Mermaid({ chart }: { chart: string }) {
  const rawId = useId().replace(/[^a-zA-Z0-9]/g, '');
  const [svg, setSvg] = useState('');

  useEffect(() => {
    let active = true;
    const dark = document.documentElement.classList.contains('dark');
    import('mermaid').then(async ({ default: mermaid }) => {
      mermaid.initialize({ startOnLoad: false, theme: dark ? 'dark' : 'neutral' });
      const { svg } = await mermaid.render(`mmd-${rawId}`, chart);
      if (active) setSvg(svg);
    });
    return () => {
      active = false;
    };
  }, [chart, rawId]);

  return (
    <div
      className="my-4 flex justify-center overflow-x-auto rounded-lg border bg-fd-card p-4"
      // biome-ignore lint/security/noDangerouslySetInnerHtml: mermaid output is trusted, authored in-repo
      dangerouslySetInnerHTML={{ __html: svg }}
    />
  );
}
