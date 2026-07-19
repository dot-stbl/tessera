import type { Story, StoryDefault } from '@ladle/react';
import { DashboardGrid, type DashboardPanel as Panel, type PanelPosition } from '@/shared/ui/apm/dashboard-grid';
import { Duration } from '@/shared/ui/apm/duration';
import { LogLevel } from '@/shared/ui/apm/log-level';
import { StatusPill } from '@/shared/ui/primitives/status-pill';

export default {
  title: 'APM / Dashboard grid',
} satisfies StoryDefault;

/**
 * 24-column Grafana-style grid. Panels positioned via
 * `pos: { x, y, w, h }`. MVP panel types: trace_list, log_view,
 * metric_chart, markdown.
 */

const makePanel = (
  id: string,
  title: string,
  pos: PanelPosition,
  body: React.ReactNode,
): Panel => ({
  id,
  pos,
  children: (
    <>
      <div className="dashboard-panel-header">
        <h3 className="dashboard-panel-title">{title}</h3>
      </div>
      <div className="dashboard-panel-body text-sm text-muted-foreground">{body}</div>
    </>
  ),
});

export const SinglePanel: Story = () => (
  <div className="p-6" style={{ height: 360 }}>
    <DashboardGrid panels={[makePanel('p1', 'Recent traces for $service', { x: 0, y: 0, w: 24, h: 8 }, 'trace_list renderer')]} />
  </div>
);

export const TwoColumns: Story = () => (
  <div className="p-6" style={{ height: 360 }}>
    <DashboardGrid panels={[
      makePanel('logs', 'Error logs',    { x: 0,  y: 0, w: 12, h: 8 }, 'log_view placeholder'),
      makePanel('p99',  'p99 latency',   { x: 12, y: 0, w: 12, h: 8 }, 'metric_chart placeholder'),
    ]} />
  </div>
);

export const FullLayout: Story = () => (
  <div className="p-6" style={{ height: 560 }}>
    <DashboardGrid panels={[
      makePanel('traces', 'Top traces',    { x: 0,  y: 0, w: 16, h: 8 }, 'trace_list'),
      makePanel('health', 'Health',        { x: 16, y: 0, w: 8,  h: 4 }, 'stats summary'),
      makePanel('err',    'Error rate',    { x: 16, y: 4, w: 8,  h: 4 }, 'metric_chart'),
      makePanel('logs',   'Logs',          { x: 0,  y: 8, w: 24, h: 8 }, 'log_view'),
    ]} />
  </div>
);

/**
 * Realistic APM dashboard with mock data — checkout service overview.
 */
export const RealisticCheckoutDashboard: Story = () => {
  const mockTraces = (
    <table className="w-full font-mono text-xs">
      <tbody>
        {[
          { id: '7f3a', svc: 'checkout-api', ms: 1247,  status: 'ok' as const },
          { id: '8c4d', svc: 'stripe',       ms: 12_400, status: 'err' as const },
          { id: '9e5f', svc: 'cart-svc',     ms: 38,    status: 'ok' as const },
          { id: 'a1b2', svc: 'pricing-svc',  ms: 1240,  status: 'warn' as const },
        ].map((r) => (
          <tr key={r.id} className="border-b border-border last:border-0">
            <td className="py-1 text-muted-foreground">{r.id}…</td>
            <td className="py-1">{r.svc}</td>
            <td className="py-1 text-right"><Duration ms={r.ms} /></td>
            <td className="py-1">
              <StatusPill variant={r.status}>{r.status === 'ok' ? 'OK' : r.status === 'err' ? 'ERROR' : 'WARN'}</StatusPill>
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );

  const mockLogs = (
    <div className="font-mono text-xs space-y-1">
      {[
        { level: 'error' as const, msg: 'card_declined', svc: 'stripe' },
        { level: 'warn'  as const, msg: 'slow query 845ms', svc: 'postgres' },
        { level: 'warn'  as const, msg: 'slow query 920ms', svc: 'postgres' },
        { level: 'info'  as const, msg: 'retrying with idempotency-key', svc: 'checkout-api' },
        { level: 'info'  as const, msg: 'POST /checkout', svc: 'checkout-api' },
      ].map((r, i) => (
        <div key={i} className="flex items-center gap-2 border-b border-border pb-1 last:border-0">
          <LogLevel level={r.level}>{r.level}</LogLevel>
          <span className="text-muted-foreground flex-1">{r.msg}</span>
          <span className="text-xs text-muted-foreground">{r.svc}</span>
        </div>
      ))}
    </div>
  );

  const mockHealth = (
    <div className="space-y-3 text-xs">
      <div className="flex items-center justify-between">
        <span>checkout-api</span>
        <StatusPill variant="err">UNHEALTHY</StatusPill>
      </div>
      <div className="flex items-center justify-between">
        <span>stripe</span>
        <StatusPill variant="warn">DEGRADED</StatusPill>
      </div>
      <div className="flex items-center justify-between">
        <span>postgres</span>
        <StatusPill variant="ok">HEALTHY</StatusPill>
      </div>
      <div className="flex items-center justify-between">
        <span>redis</span>
        <StatusPill variant="ok">HEALTHY</StatusPill>
      </div>
    </div>
  );

  const mockMetrics = (
    <div className="space-y-3 text-xs">
      <div>
        <div className="text-muted-foreground">p50</div>
        <div className="font-mono text-lg"><Duration ms={12} /></div>
      </div>
      <div>
        <div className="text-muted-foreground">p95</div>
        <div className="font-mono text-lg"><Duration ms={245} /></div>
      </div>
      <div>
        <div className="text-muted-foreground">p99</div>
        <div className="font-mono text-lg text-err-ink"><Duration ms={1240} /></div>
      </div>
      <div className="pt-2 border-t border-border">
        <div className="text-muted-foreground">Error rate (5m)</div>
        <div className="font-mono text-lg text-err-ink">14.2%</div>
      </div>
    </div>
  );

  return (
    <div className="p-6" style={{ height: 620 }}>
      <DashboardGrid panels={[
        makePanel('traces', 'Top traces',  { x: 0,  y: 0, w: 16, h: 10 }, mockTraces),
        makePanel('health', 'Health',      { x: 16, y: 0, w: 8,  h: 10 }, mockHealth),
        makePanel('logs',   'Recent logs', { x: 0,  y: 10, w: 16, h: 8 }, mockLogs),
        makePanel('metrics','Latency + errors', { x: 16, y: 10, w: 8,  h: 8 }, mockMetrics),
      ]} />
    </div>
  );
};