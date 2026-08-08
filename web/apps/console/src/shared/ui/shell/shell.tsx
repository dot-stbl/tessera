import type { ReactNode } from 'react';
import { TimeWindowProvider } from '@/shared/lib/time-window';
import { Bar } from './bar';
import { Side, SideProvider } from './side';
import { Spine } from './spine';

/**
 * The chrome.
 *
 *   bar    — where you are, and the few things true on every screen
 *   spine  — when you are looking at; global, and it does not reset on navigate
 *   side   — how to narrow the current screen
 *   main   — the screen
 *
 * The providers wrap the whole tree rather than the individual bands because
 * the screen inside `main` is what feeds both the spine and the sidebar. That
 * inversion — chrome renders, screen supplies — is what lets one band describe
 * traces, logs and services without changing what it means.
 */
export function Shell({ children }: { children: ReactNode }) {
  return (
    <TimeWindowProvider>
      <SideProvider>
        <div className="shell">
          <a href="#main" className="skip">
            Skip to content
          </a>
          <Bar />
          <Spine />
          <Side />
          <main id="main" className="main" tabIndex={-1}>
            {children}
          </main>
        </div>
      </SideProvider>
    </TimeWindowProvider>
  );
}
