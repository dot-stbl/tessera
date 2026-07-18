import { type ReactNode } from 'react';
import { cn } from '@/shared/lib/utils';

export interface PanelPosition {
  x: number;
  y: number;
  w: number;
  h: number;
}

export interface DashboardPanel {
  id: string;
  /** Grid position. */
  pos: PanelPosition;
  /** Panel content. */
  children: ReactNode;
}

export interface DashboardGridProps {
  panels: DashboardPanel[];
  /** Total grid columns. Default 24. */
  columns?: number;
  /** Row height in pixels. Default 60. */
  rowHeight?: number;
  /** Gap between panels in pixels. Default 12. */
  gap?: number;
  className?: string;
}

/**
 * 24-column dashboard grid. Panels position themselves by grid pos
 * {x, y, w, h}. Auto-layout — no drag-resize in MVP (that's a stretch).
 *
 * Usage:
 *   <DashboardGrid panels={[
 *     { id: 'p1', pos: { x: 0, y: 0, w: 24, h: 4 }, children: <TraceListPanel /> },
 *     { id: 'p2', pos: { x: 0, y: 4, w: 12, h: 8 }, children: <LogViewPanel /> },
 *     { id: 'p3', pos: { x: 12, y: 4, w: 12, h: 8 }, children: <ChartPanel /> },
 *   ]} />
 */
export function DashboardGrid({
  panels,
  columns = 24,
  rowHeight = 60,
  gap = 12,
  className,
}: DashboardGridProps) {
  return (
    <div
      className={cn('dashboard-grid', className)}
      style={{
        display: 'grid',
        gridTemplateColumns: `repeat(${columns}, 1fr)`,
        gridAutoRows: `${rowHeight}px`,
        gap: `${gap}px`,
      }}
    >
      {panels.map((panel) => (
        <div
          key={panel.id}
          className={cn('dashboard-panel', `dashboard-panel-w${panel.pos.w}`)}
          style={{
            gridColumn: `${panel.pos.x + 1} / span ${panel.pos.w}`,
            gridRow: `${panel.pos.y + 1} / span ${panel.pos.h}`,
          }}
        >
          {panel.children}
        </div>
      ))}
    </div>
  );
}