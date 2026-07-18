import { type ReactNode } from 'react';
import { cn } from '@/shared/lib/utils';

export interface SpanRowProps {
  /** Span name (operation). */
  name: string;
  /** Service that produced this span. */
  service: string;
  /** Span duration in milliseconds. */
  durationMs: number;
  /** Start time offset from trace start, in milliseconds. */
  startOffsetMs: number;
  /** Total trace duration in milliseconds (for percentage calc). */
  traceDurationMs: number;
  /** Span depth in the parent/child tree. 0 = root. */
  depth?: number;
  /** Span status for coloring. */
  status?: 'ok' | 'error' | 'unset';
  /** Click handler — opens span detail panel. */
  onClick?: () => void;
  /** Indicate this span is currently selected (highlighted). */
  isSelected?: boolean;
  children?: ReactNode;
}

/**
 * Single span in the trace waterfall.
 *
 * Position: bar starts at `(startOffsetMs / traceDurationMs) * 100%` and
 * extends for `(durationMs / traceDurationMs) * 100%` of the trace width.
 *
 * Status colors:
 *   - `ok`     — soft green background
 *   - `error`  — soft red background + indicator
 *   - `unset`  — neutral surface
 *
 * Usage:
 *   <SpanRow
 *     name="POST /checkout"
 *     service="checkout-api"
 *     durationMs={1247}
 *     startOffsetMs={0}
 *     traceDurationMs={1500}
 *     depth={0}
 *     status="error"
 *     onClick={() => setSelectedSpan(span)}
 *   />
 */
export function SpanRow({
  name,
  service,
  durationMs,
  startOffsetMs,
  traceDurationMs,
  depth = 0,
  status = 'unset',
  onClick,
  isSelected,
}: SpanRowProps) {
  const offsetPct = (startOffsetMs / traceDurationMs) * 100;
  const widthPct = Math.max((durationMs / traceDurationMs) * 100, 0.3);

  return (
    <div
      className={cn(
        'span-row',
        `span-row-status-${status}`,
        isSelected && 'span-row-selected',
      )}
      data-depth={depth}
      onClick={onClick}
      role={onClick ? 'button' : undefined}
      tabIndex={onClick ? 0 : undefined}
    >
      <div className="span-label">
        <span className="span-service">{service}</span>
        <span className="span-name">{name}</span>
      </div>
      <div className="span-track">
        <div
          className={cn('span-bar', `span-bar-${status}`)}
          style={{
            marginLeft: `${offsetPct}%`,
            width: `${widthPct}%`,
          }}
        >
          {status === 'error' && <span className="span-bar-error-icon" aria-label="error">!</span>}
        </div>
      </div>
      <div className="span-duration">
        {durationMs < 1 ? '<1ms' : `${durationMs.toFixed(0)}ms`}
      </div>
    </div>
  );
}