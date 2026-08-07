import { type ReactNode } from 'react';
import { Link, useLocation } from '@tanstack/react-router';
import { useQuery } from '@tanstack/react-query';
import { api } from '@/shared/api';
import { APP_SECTIONS, APP_SECTION_BY_PATH } from './nav-config';

export interface AppShellProps {
  children: ReactNode;
}

/**
 * Tessera app shell.
 *
 *   ┌────┬──────────────────────────────┐
 *   │    │ path · · · · · · · · health  │
 *   │rail├──────────────────────────────┤
 *   │52px│ main                         │
 *   └────┴──────────────────────────────┘
 *
 * Navigation is an icon rail, not a labelled sidebar. Five destinations that an
 * operator learns in a day do not need 220px of text on every screen; the
 * timeline does need those pixels. Names appear on hover and on keyboard focus.
 *
 * The top bar carries the two things that hold regardless of which screen you are
 * on: where you are, and whether the providers are answering.
 */
export function AppShell({ children }: AppShellProps) {
  const location = useLocation();
  const currentPath = location.pathname;
  const currentSection =
    APP_SECTION_BY_PATH.get(currentPath) ??
    APP_SECTIONS.find((section) => currentPath.startsWith(`${section.to}/`));

  return (
    <div className="app-shell">
      <nav className="app-sidebar" aria-label="Main navigation">
        <Link to="/" className="app-sidebar-brand" aria-label="Tessera home">
          <svg width="18" height="18" viewBox="0 0 32 32" aria-hidden="true">
            <rect x="9" y="3" width="6" height="6" fill="currentColor" />
            <rect x="21" y="14" width="6" height="6" fill="currentColor" />
            <rect x="5" y="22" width="6" height="6" fill="currentColor" />
            <rect x="12" y="22" width="6" height="6" fill="var(--primary)" />
          </svg>
        </Link>

        {APP_SECTIONS.map((section) => (
          <Link
            key={section.id}
            to={section.to}
            className={
              currentSection?.id === section.id
                ? 'app-sidebar-section is-active'
                : 'app-sidebar-section'
            }
            aria-current={currentSection?.id === section.id ? 'page' : undefined}
          >
            {section.icon}
            {/* Shown on hover and focus; still the link's accessible name. */}
            <span>{section.label}</span>
          </Link>
        ))}
      </nav>

      <header className="app-topnav">
        <AppPath path={currentPath} section={currentSection?.label} />
        <span className="app-topnav-spacer" />
        <ProviderHealth />
      </header>

      <main className="app-main">{children}</main>
    </div>
  );
}

/**
 * Where you are, as a path rather than a sentence. The trace id is the tail and is
 * allowed to truncate — it is the least readable and most copyable part, and the
 * page title repeats the operation name in full underneath.
 */
function AppPath({ path, section }: { path: string; section: string | undefined }) {
  const tail = path.split('/').filter(Boolean).slice(1).join('/');

  return (
    <div className="app-path">
      <b>{section ?? 'Tessera'}</b>
      {tail && (
        <>
          <span className="sep">/</span>
          <span className="id">{tail}</span>
        </>
      )}
    </div>
  );
}

/**
 * Provider health: one dot, one word. The question is only ever "is the data I am
 * looking at complete", and the per-provider detail belongs in Settings rather
 * than in the chrome of every screen.
 */
function ProviderHealth() {
  const { data, isError } = useQuery({
    queryKey: ['health'],
    queryFn: ({ signal }) => api.getHealth(signal),
    refetchInterval: 30_000,
    staleTime: 15_000,
  });

  const status = isError ? 'unhealthy' : data?.status;

  return (
    <span className="app-health" data-status={status ?? 'unknown'} title={data?.detail ?? undefined}>
      <i aria-hidden="true" />
      {status ?? 'checking'}
    </span>
  );
}
