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
 * Provider health — and it renders **nothing at all while the providers are
 * healthy**, which is almost always.
 *
 * A permanently green "● HEALTHY" chip is not information. It costs a fixed
 * corner of every screen to restate the default, and it trains the eye to skip
 * the one place that would have said the trace store stopped answering. Chrome
 * is silent until it has something to say; when it does, it says which provider
 * and what is wrong, and the detail lives in Settings.
 */
function ProviderHealth() {
  const { data, isError } = useQuery({
    queryKey: ['health'],
    queryFn: ({ signal }) => api.getHealth(signal),
    refetchInterval: 30_000,
    staleTime: 15_000,
  });

  // Undefined means the first check has not returned. That is not a fault, and
  // flashing "checking" on every page load is the same noise in a paler colour.
  const status = isError ? 'unhealthy' : data?.status;
  if (status === undefined || status === 'healthy') return null;

  return (
    <span className="app-health" data-status={status} title={data?.detail ?? undefined}>
      <i aria-hidden="true" />
      {isError ? 'provider unreachable' : `providers ${status}`}
    </span>
  );
}
