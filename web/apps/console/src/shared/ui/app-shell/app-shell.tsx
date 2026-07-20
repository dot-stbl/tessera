import { type ReactNode } from 'react';
import { Link, useLocation } from '@tanstack/react-router';
import { APP_SECTIONS, APP_SECTION_BY_PATH } from './nav-config';
import { APP_NAME } from '@/shared/lib/app-name';

export interface AppShellProps {
  children: ReactNode;
}

/**
 * Tessera app shell — topnav + sidebar + main content area.
 *
 * Layout:
 *   ┌─────────────────────────────────┐
 *   │ topnav (brand · sections ...)    │
 *   ├─────────┬───────────────────────┤
 *   │ sidebar │ main                  │
 *   │         │                       │
 *   └─────────┴───────────────────────┘
 *
 * Active section is detected from the current path. Brand links to home.
 */
export function AppShell({ children }: AppShellProps) {
  const location = useLocation();
  const currentPath = location.pathname;
  const currentSection = APP_SECTION_BY_PATH.get(currentPath);

  return (
    <div className="app-shell">
      <header className="app-topnav">
        <Link to="/" className="app-topnav-brand">
          <svg width="20" height="20" viewBox="0 0 32 32" aria-hidden="true">
            <rect x="9"  y="3"  width="6" height="6" fill="currentColor"/>
            <rect x="21" y="14" width="6" height="6" fill="currentColor"/>
            <rect x="5"  y="22" width="6" height="6" fill="currentColor"/>
            <rect x="12" y="22" width="6" height="6" fill="var(--primary)"/>
          </svg>
          <span>{APP_NAME}</span>
        </Link>
        <span className="app-topnav-spacer" />
      </header>

      <nav className="app-sidebar" aria-label="Main navigation">
        {APP_SECTIONS.map((section) => (
          <Link
            key={section.id}
            to={section.to}
            className={
              currentSection?.id === section.id || currentPath.startsWith(`${section.to}/`)
                ? 'app-sidebar-section is-active'
                : 'app-sidebar-section'
            }
          >
            {section.icon}
            <span>{section.label}</span>
          </Link>
        ))}
      </nav>

      <main className="app-main">{children}</main>
    </div>
  );
}