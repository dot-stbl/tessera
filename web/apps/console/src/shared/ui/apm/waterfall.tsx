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
  children?: WaterfallSpan[];
}

export interface WaterfallProps {
  spans: WaterfallSpan[];
  /** Total trace duration in ms (used to compute bar widths). */
  totalDurationMs: number;
  /** Currently selected span id. */
  selectedSpanId?: string;
  onSpanClick?: (span: WaterfallSpan) => void;
  className?: string;
}

/**
 * Trace waterfall — Jaeger-style timeline of parent/child spans.
 * Renders spans in DFS order so child bars indent under their parent.
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
      <div className="waterfall-header">
        <div className="waterfall-label">Service · Operation</div>
        <div className="waterfall-track" />
        <div className="waterfall-duration">Duration</div>
      </div>
      <div className="waterfall-body">
        {spans.map((span) => (
          <WaterfallNode
            key={span.id}
            span={span}
            totalDurationMs={totalDurationMs}
            selectedSpanId={selectedSpanId}
            onSpanClick={onSpanClick}
          />
        ))}
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