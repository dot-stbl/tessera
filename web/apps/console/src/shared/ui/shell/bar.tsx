import { Link, useLocation } from '@tanstack/react-router';
import { DarkMode, LightMode } from '@nine-thirty-five/material-symbols-react/rounded/400';
import { usePreferences } from '@/shared/lib/preferences-provider';
import { cn } from '@/shared/lib/utils';
import { Mark } from './mark';

/**
 * The top bar: where you are, and the handful of things that are true on every
 * screen.
 *
 * Horizontal, because there are five destinations and they are shallow — a
 * vertical rail spent 176px of every screen restating a list that fits in one
 * strip. The sidebar underneath is not a second copy of this: this answers
 * "where am I", the sidebar answers "narrow it down".
 *
 * The name is lowercase because every brand asset spells it that way — the
 * wordmark, the lockup, the OG card, the page title. The running app was the
 * only place that capitalised it.
 */

interface Section {
  to: string;
  label: string;
}

const SECTIONS: Section[] = [
  { to: '/traces', label: 'traces' },
  { to: '/logs', label: 'logs' },
  { to: '/services', label: 'services' },
  { to: '/dashboards', label: 'dashboards' },
  { to: '/settings', label: 'settings' },
];

export function Bar() {
  const { pathname } = useLocation();
  const { preferences, update } = usePreferences();
  const dark = preferences.theme !== 'light';

  return (
    <header className="bar">
      <Link to="/traces" className="bar-brand">
        <Mark size={16} />
        tessera
      </Link>

      <nav className="bar-nav" aria-label="Sections">
        {SECTIONS.map((section) => {
          const active =
            pathname === section.to || pathname.startsWith(`${section.to}/`);
          return (
            <Link
              key={section.to}
              to={section.to}
              className={cn('bar-section', active && 'is-active')}
              aria-current={active ? 'page' : undefined}
            >
              {section.label}
            </Link>
          );
        })}
      </nav>

      <span className="bar-spacer" />

      <div className="bar-tools">
        <button
          type="button"
          className="iconbtn"
          aria-label={dark ? 'Switch to the light theme' : 'Switch to the dark theme'}
          onClick={() => update('theme', dark ? 'light' : 'dark')}
        >
          {dark ? <LightMode /> : <DarkMode />}
        </button>
      </div>
    </header>
  );
}
