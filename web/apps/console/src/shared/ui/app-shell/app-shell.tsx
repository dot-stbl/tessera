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
 *   ┌─────────┬─────────────────────────┐
 *   │ sidebar │ path · · · · · · health │
 *   │  176px  ├─────────────────────────┤
 *   │         │ main                    │
 *   └─────────┴─────────────────────────┘
 *
 * The sidebar was a 52px icon rail, on the argument that five destinations an
 * operator learns in a day do not need 220px of text on every screen and the
 * timeline does need those pixels. That trade was wrong: an icon-only nav has
 * no discoverability, the names were reachable only by hovering, and 124px is
 * not what makes or breaks a waterfall. Icon *and* label, as every navigation
 * guideline says.
 *
 * The top bar carries where you are, and — only when there is something to say —
 * whether the providers are answering.
 */
export function AppShell({ children }: AppShellProps) {
  const location = useLocation();
  const currentPath = location.pathname;
  const currentSection =
    APP_SECTION_BY_PATH.get(currentPath) ??
    APP_SECTIONS.find((section) => currentPath.startsWith(`${section.to}/`));

  return (
    <div className="app-shell">
      {/* Six tab stops sit between the address bar and the first row of data on
       * every page load. This is the way past them. */}
      <a href="#main" className="skip-link">
        Skip to content
      </a>

      <nav className="app-sidebar" aria-label="Main navigation">
        <Link to="/" className="app-sidebar-brand">
          <svg width="18" height="18" viewBox="0 0 32 32" aria-hidden="true">
            <rect x="9" y="3" width="6" height="6" fill="currentColor" />
            <rect x="21" y="14" width="6" height="6" fill="currentColor" />
            <rect x="5" y="22" width="6" height="6" fill="currentColor" />
            <rect x="12" y="22" width="6" height="6" fill="var(--primary)" />
          </svg>
          Tessera
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
            <span>{section.label}</span>
          </Link>
        ))}
      </nav>

      <header className="app-topnav">
        <AppPath path={currentPath} section={currentSection?.label} />
        <span className="app-topnav-spacer" />
        <ProviderHealth />
      </header>

      {/* tabIndex -1 so the skip link can land here; it is not in the tab order. */}
      <main id="main" className="app-main" tabIndex={-1}>
        {children}
      </main>
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
