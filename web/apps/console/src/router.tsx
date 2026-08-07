import {
  createRootRoute,
  createRoute,
  createRouter,
  Outlet,
  redirect,
} from '@tanstack/react-router';
import { AppShell, PageTemplate } from '@/shared/ui/app-shell';
import { TracesPage } from '@/features/traces/traces-page';
import { TraceDetailPage } from '@/features/traces/trace-detail-page';
import { LogsPage } from '@/features/logs/logs-page';
import { ServicesPage } from '@/features/services/services-page';
import { DashboardsPage } from '@/features/dashboards/dashboards-page';
import {
  parseOptionalString,
  parseTimeRange,
  type TimeRangeKey,
} from '@/shared/lib/search-params';

/**
 * Code-based route definitions. Simpler than file-based for MVP — no codegen
 * step required, no `routeTree.gen.ts` to commit. Switch to file-based once
 * the route count grows past ~10.
 */

// ─── Page components ───────────────────────────────────────────────────

// All page components are imported from @/features/<section>/<name>-page.
// TracesPage, LogsPage, ServicesPage, DashboardsPage.

function SettingsPage() {
  return (
    <PageTemplate title="Settings" subtitle="Victoria endpoints, theme, preferences">
      <div className="rounded-md border border-border bg-card p-8 text-center text-sm text-muted-foreground">
        Settings UI — coming in MVP phase 1.
      </div>
    </PageTemplate>
  );
}

function NotFoundPage() {
  return (
    <PageTemplate title="Not found" subtitle="The page you're looking for doesn't exist.">
      <a href="/traces" className="text-sm text-muted-foreground underline">
        Back to traces
      </a>
    </PageTemplate>
  );
}

// ─── Root route ───────────────────────────────────────────────────────

const rootRoute = createRootRoute({
  component: function RootLayout() {
    return (
      <AppShell>
        <Outlet />
      </AppShell>
    );
  },
  notFoundComponent: NotFoundPage,
});

// ─── Index → /traces ──────────────────────────────────────────────────

const indexRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/',
  beforeLoad: () => {
    throw redirect({ to: '/traces' });
  },
});

// ─── Section routes ───────────────────────────────────────────────────

/**
 * Search params carry the view state, so what an operator is looking at is
 * addressable: the link pasted into an incident channel opens on the same
 * filters, the same trace and the same span, and a reload does not throw the
 * investigation away. Filters living in component state made every view
 * unshareable and every reload a restart.
 */

export interface TracesSearch {
  service?: string;
  /**
   * Optional in the type, always populated by the parser. Declaring it required
   * would force every `<Link to="/traces">` in the app — including the rail — to
   * restate the default range, so the type stays permissive for callers and pages
   * read it through the same DEFAULT_TIME_RANGE constant the parser uses.
   */
  range?: TimeRangeKey;
  /**
   * Which table implementation renders the list. A spike switch: `aria` selects
   * the React Aria Components build so the two can be compared without a rebuild.
   * Goes away together with one of the implementations once decided.
   */
  table?: 'aria';
}

const tracesRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/traces',
  component: TracesPage,
  validateSearch: (search: Record<string, unknown>): TracesSearch => ({
    service: parseOptionalString(search['service']),
    range: parseTimeRange(search['range']),
    table: search['table'] === 'aria' ? 'aria' : undefined,
  }),
});

export interface TraceDetailSearch {
  /** Selected span id. Drives both the highlight and the log filter. */
  span?: string;
}

const traceDetailRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/traces/$traceId',
  component: TraceDetailPage,
  validateSearch: (search: Record<string, unknown>): TraceDetailSearch => ({
    span: parseOptionalString(search['span']),
  }),
});

export interface LogsSearch {
  stream?: string;
  traceId?: string;
  /** Optional for callers, populated by the parser — see {@link TracesSearch}. */
  range?: TimeRangeKey;
}

const logsRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/logs',
  component: LogsPage,
  validateSearch: (search: Record<string, unknown>): LogsSearch => ({
    stream: parseOptionalString(search['stream']),
    traceId: parseOptionalString(search['traceId']),
    range: parseTimeRange(search['range']),
  }),
});

const servicesRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/services',
  component: ServicesPage,
});

const dashboardsRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/dashboards',
  component: DashboardsPage,
});

const settingsRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/settings',
  component: SettingsPage,
});

// ─── Router ────────────────────────────────────────────────────────────

const routeTree = rootRoute.addChildren([
  indexRoute,
  tracesRoute,
  traceDetailRoute,
  logsRoute,
  servicesRoute,
  dashboardsRoute,
  settingsRoute,
]);

export const router = createRouter({
  routeTree,
  defaultPreload: 'intent',
  defaultPreloadStaleTime: 0,
});

declare module '@tanstack/react-router' {
  interface Register {
    router: typeof router;
  }
}