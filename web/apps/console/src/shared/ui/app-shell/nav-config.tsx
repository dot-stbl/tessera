import type { ReactNode } from 'react';

/**
 * Tessera sidebar sections. Each section maps to a top-level route.
 * Icons are inline SVGs (minimal dependency footprint for MVP); swap to
 * lucide-react or hugeicons later.
 */

export interface AppSection {
  id: string;
  /** Route path (TanStack Router). */
  to: string;
  /** Display label — can be i18n key or literal string. */
  label: string;
  /** Inline SVG icon. */
  icon: ReactNode;
}

const Icon = ({ d }: { d: string }) => (
  <svg viewBox="0 0 16 16" fill="none" aria-hidden="true">
    <path d={d} stroke="currentColor" strokeWidth="1.3" strokeLinecap="round" strokeLinejoin="round" />
  </svg>
);

export const APP_SECTIONS: AppSection[] = [
  {
    id: 'traces',
    to: '/traces',
    label: 'Traces',
    icon: <Icon d="M2 13 5 9l3 3 6-7 M9 5h5v5" />,
  },
  {
    id: 'logs',
    to: '/logs',
    label: 'Logs',
    icon: <Icon d="M3 3h10M3 6h10M3 9h7M3 12h10" />,
  },
  {
    id: 'services',
    to: '/services',
    label: 'Services',
    icon: <Icon d="M3 5h4v4H3zM9 5h4v4H9zM3 11h4v4H3zM9 11h4v4H9z" />,
  },
  {
    id: 'dashboards',
    to: '/dashboards',
    label: 'Dashboards',
    icon: <Icon d="M2 2h5v5H2zM9 2h5v5H9zM2 9h5v5H2zM9 9h5v5H9z" />,
  },
  {
    id: 'settings',
    to: '/settings',
    label: 'Settings',
    icon: <Icon d="M8 1v2M8 13v2M1 8h2M13 8h2M3 3l1.4 1.4M11.6 11.6 1.4 1.4M3 13l1.4-1.4M11.6 4.4l1.4-1.4" />,
  },
];

export const APP_SECTION_BY_PATH = new Map(APP_SECTIONS.map((s) => [s.to, s]));