import {
  createRootRoute,
  createRoute,
  createRouter,
  Outlet,
  redirect,
} from '@tanstack/react-router';
import { AppShell, PageTemplate } from '@/shared/ui/app-shell';
import { TracesPage } from '@/features/traces/traces-page';

/**
 * Code-based route definitions. Simpler than file-based for MVP — no codegen
 * step required, no `routeTree.gen.ts` to commit. Switch to file-based once
 * the route count grows past ~10.
 */

// ─── Page components ───────────────────────────────────────────────────

// TracesPage is now imported from @/features/traces/traces-page.
// Other sections are still placeholders (filled in MVP phase 2-4).

function LogsPage() {
  return (
    <PageTemplate title="Logs" subtitle="Structured log search and viewer">
      <div className="rounded-md border border-border bg-card p-8 text-center text-sm text-muted-foreground">
        Log viewer — coming in MVP phase 3.
      </div>
    </PageTemplate>
  );
}

function ServicesPage() {
  return (
    <PageTemplate title="Services" subtitle="Service inventory from VictoriaTraces">
      <div className="rounded-md border border-border bg-card p-8 text-center text-sm text-muted-foreground">
        Service inventory — coming in MVP phase 1.
      </div>
    </PageTemplate>
  );
}

function DashboardsPage() {
  return (
    <PageTemplate title="Dashboards" subtitle="Custom Grafana-style dashboards">
      <div className="rounded-md border border-border bg-card p-8 text-center text-sm text-muted-foreground">
        Dashboards — coming in MVP phase 4.
      </div>
    </PageTemplate>
  );
}

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

const tracesRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/traces',
  component: TracesPage,
});

const logsRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/logs',
  component: LogsPage,
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