import { type ReactNode } from 'react';
import { cn } from '@/shared/lib/utils';
import { SpanRow, type SpanRowMarker, type SpanRowProps } from './span-row';

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
  /**
   * Notable logs to draw on the timeline, keyed by the span they were emitted
   * in. Entries whose span is absent from `spans` are simply not drawn — a log
   * is never attached to a span it did not come from.
   */
  markersBySpanId?: Map<string, SpanRowMarker[]>;
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
  markersBySpanId,
  className,
}: WaterfallProps) {
  return (
    <div className={cn('waterfall', className)}>
      <div className="waterfall-axis">
        <div className="waterfall-axis-offset">at</div>
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
        <div className="waterfall-axis-dur">Took</div>
      </div>
      <div className="waterfall-body">
        {spans.length === 0 ? (
          <div className="waterfall-empty">This trace has no spans. Its logs are below.</div>
        ) : (
          spans.map((span) => (
            <WaterfallNode
              key={span.id}
              span={span}
              totalDurationMs={totalDurationMs}
              selectedSpanId={selectedSpanId}
              onSpanClick={onSpanClick}
              markersBySpanId={markersBySpanId}
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
  markersBySpanId?: Map<string, SpanRowMarker[]>;
}

function WaterfallNode({
  span,
  totalDurationMs,
  selectedSpanId,
  onSpanClick,
  markersBySpanId,
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
    markers: markersBySpanId?.get(span.id),
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
          markersBySpanId={markersBySpanId}
        />
      ))}
    </>
  );
}
