import { type ReactNode } from 'react';
import { cn } from '@/shared/lib/utils';
import { SpanRow, type SpanRowProps } from './span-row';

export interface WaterfallSpan {
  id: string;
  name: string;
  service: string;
  durationMs: number;
  startOffsetMs: number;
  depth?: number;
  status?: 'ok' | 'error' | 'unset';
  /** Mark this span as part of the trace's critical path. */
  isCritical?: boolean;
  children?: WaterfallSpan[];
}

export interface WaterfallProps {
  spans: WaterfallSpan[];
  /** Total trace duration in ms (used to compute bar widths + axis ticks). */
  totalDurationMs: number;
  /** Currently selected span id. */
  selectedSpanId?: string;
  onSpanClick?: (span: WaterfallSpan) => void;
  className?: string;
}

const TICKS = [0, 0.25, 0.5, 0.75, 1];

function formatTick(ms: number): string {
  if (ms <= 0) return '0';
  if (ms < 1000) return `${Math.round(ms)}ms`;
  return `${(ms / 1000).toFixed(ms < 10_000 ? 2 : 1)}s`;
}

/**
 * Trace waterfall — Jaeger / Kibana APM style timeline of parent/child spans.
 * Renders spans in DFS order so child bars indent under their parent. The
 * header is a time axis with tick labels; each row's track carries faint
 * gridlines aligned to the ticks.
 *
 * Usage:
 *   <Waterfall
 *     spans={trace.spans}
 *     totalDurationMs={trace.durationMs}
 *     selectedSpanId={selected?.id}
 *     onSpanClick={(s) => setSelected(s)}
 *   />
 */
export function Waterfall({
  spans,
  totalDurationMs,
  selectedSpanId,
  onSpanClick,
  className,
}: WaterfallProps) {
  return (
    <div className={cn('waterfall', className)}>
      <div className="waterfall-axis">
        <div className="waterfall-axis-label">Service · Operation</div>
        <div className="waterfall-axis-track">
          {TICKS.map((t, i) => (
            <span
              key={t}
              className={cn(
                'waterfall-tick',
                i === 0 && 'is-first',
                i === TICKS.length - 1 && 'is-last',
              )}
              style={{ left: `${t * 100}%` }}
            >
              {formatTick(totalDurationMs * t)}
            </span>
          ))}
        </div>
        <div className="waterfall-axis-dur">Duration</div>
      </div>
      <div className="waterfall-body">
        {spans.length === 0 ? (
          <div className="waterfall-empty">No spans in this trace.</div>
        ) : (
          spans.map((span) => (
            <WaterfallNode
              key={span.id}
              span={span}
              totalDurationMs={totalDurationMs}
              selectedSpanId={selectedSpanId}
              onSpanClick={onSpanClick}
            />
          ))
        )}
      </div>
    </div>
  );
}

interface WaterfallNodeProps {
  span: WaterfallSpan;
  totalDurationMs: number;
  selectedSpanId?: string;
  onSpanClick?: (span: WaterfallSpan) => void;
}

function WaterfallNode({
  span,
  totalDurationMs,
  selectedSpanId,
  onSpanClick,
}: WaterfallNodeProps): ReactNode {
  const rowProps: SpanRowProps = {
    name: span.name,
    service: span.service,
    durationMs: span.durationMs,
    startOffsetMs: span.startOffsetMs,
    traceDurationMs: totalDurationMs,
    depth: span.depth ?? 0,
    status: span.status,
    isCritical: span.isCritical,
    isSelected: span.id === selectedSpanId,
    onClick: onSpanClick ? () => onSpanClick(span) : undefined,
  };

  return (
    <>
      <SpanRow {...rowProps} />
      {span.children?.map((child) => (
        <WaterfallNode
          key={child.id}
          span={child}
          totalDurationMs={totalDurationMs}
          selectedSpanId={selectedSpanId}
          onSpanClick={onSpanClick}
        />
      ))}
    </>
  );
}
