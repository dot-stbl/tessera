import type { Story, StoryDefault } from '@ladle/react';
import { DashboardGrid, type DashboardPanel as Panel, type PanelPosition } from '@/shared/ui/apm/dashboard-grid';

export default {
  title: 'APM / Dashboard grid',
} satisfies StoryDefault;

/**
 * Dashboard grid — 24-column Grafana-style layout. Panels are positioned
 * via `pos: { x, y, w, h }` and rendered as `<DashboardPanel>` children
 * passed to `<DashboardGrid>`. MVP panel types: trace_list, log_view,
 * metric_chart, markdown.
 */

const makePanel = (
  id: string,
  title: string,
  pos: PanelPosition,
  body: string,
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