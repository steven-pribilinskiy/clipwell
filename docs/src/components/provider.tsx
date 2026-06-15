'use client';
import SearchDialog from '@/components/search';
import { RootProvider } from 'fumadocs-ui/provider/next';
import { type ReactNode } from 'react';

export function Provider({ children }: { children: ReactNode }) {
  // Light/dark with the OS preference as the default (users can still toggle).
  return (
    <RootProvider
      search={{ SearchDialog }}
      theme={{ enabled: true, enableSystem: true, defaultTheme: 'system' }}
    >
      {children}
    </RootProvider>
  );
}
