import {
  createRootRoute,
  createRoute,
  createRouter,
  Link,
  Outlet,
  redirect,
} from '@tanstack/react-router';
import { PageTemplate } from '@/shared/ui/app-shell';
import { Shell } from '@/shared/ui/shell';
import { Blank, BlankText } from '@/shared/ui/console';
import { SettingsPage } from '@/features/settings/settings-page';
import { TracesPage } from '@/features/traces/traces-page';
import { TraceDetailPage } from '@/features/traces/trace-detail-page';
import { LogsPage } from '@/features/logs/logs-page';
import { ServicesPage } from '@/features/services/services-page';
import { DashboardsPage } from '@/features/dashboards/dashboards-page';
import {
  parseOptionalString,
  parseTimeRange,
  parseTraceSort,
  parseTraceStatusFilter,
  type TimeRangeKey,
  type TraceSort,
  type TraceStatusFilter,
} from '@/shared/lib/search-params';

/**
 * Code-based route definitions. Simpler than file-based for MVP — no codegen
 * step required, no `routeTree.gen.ts` to commit. Switch to file-based once
 * the route count grows past ~10.
 */

// ─── Page components ───────────────────────────────────────────────────

// All page components are imported from @/features/<section>/<name>-page.
// TracesPage, LogsPage, ServicesPage, DashboardsPage.

function NotFoundPage() {
  return (
    <PageTemplate title="Not found" subtitle="No route matches this address">
      <Blank title="There is nothing at this address.">
        <BlankText>
          A trace link goes stale once its retention window passes. If you followed one from a
          ticket or an alert, the trace itself may have expired rather than the page being wrong.
        </BlankText>
        <BlankText>
          <Link to="/traces">Back to traces</Link>
        </BlankText>
      </Blank>
    </PageTemplate>
  );
}

// ─── Root route ───────────────────────────────────────────────────────

const rootRoute = createRootRoute({
  component: function RootLayout() {
    return (
      <Shell>
        <Outlet />
      </Shell>
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
  /**
   * Narrows the list to failing or slow requests. Kept in the URL like every
   * other filter: "the errors in the last 6h" is the thing people paste into an
   * incident channel, and it has to survive being pasted.
   */
  status?: TraceStatusFilter;
  /** Column order. See TRACE_SORTS — named pairs, so the URL stays readable. */
  sort?: TraceSort;
}

const tracesRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/traces',
  component: TracesPage,
  validateSearch: (search: Record<string, unknown>): TracesSearch => ({
    service: parseOptionalString(search['service']),
    range: parseTimeRange(search['range']),
    table: search['table'] === 'aria' ? 'aria' : undefined,
    status: parseTraceStatusFilter(search['status']),
    sort: parseTraceSort(search['sort']),
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