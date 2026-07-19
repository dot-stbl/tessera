import { type CSSProperties, type ReactNode } from 'react';
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
  /** Span depth in the parent/child tree. 0 = root. Any depth is supported. */
  depth?: number;
  /** Span status. `error` overrides the bar to red + a flag. */
  status?: 'ok' | 'error' | 'unset';
  /** Mark this span as part of the trace's critical path. */
  isCritical?: boolean;
  /** Click handler — opens span detail panel. */
  onClick?: () => void;
  /** Indicate this span is currently selected (highlighted). */
  isSelected?: boolean;
  children?: ReactNode;
}

/**
 * Stable 1..6 service→palette index derived from the service name, so a
 * given service keeps the same colour across every span and every trace.
 */
export function serviceColorIndex(service: string): number {
  let hash = 0;
  for (let i = 0; i < service.length; i += 1) {
    hash = (Math.imul(hash, 31) + service.charCodeAt(i)) >>> 0;
  }
  return (hash % 6) + 1;
}

/**
 * Single span in the trace waterfall (Jaeger / Kibana APM style).
 *
 * The bar starts at `(startOffsetMs / traceDurationMs)` and spans
 * `(durationMs / traceDurationMs)` of the track. Bars are coloured by
 * **service** (the categorical `--svc-*` palette) so timing structure reads
 * at a glance; error spans override to red with a flag. Depth renders as
 * indentation and works at any depth.
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
 *     isCritical
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
  isCritical = false,
  onClick,
  isSelected = false,
}: SpanRowProps) {
  const offsetPct = traceDurationMs > 0 ? (startOffsetMs / traceDurationMs) * 100 : 0;
  const widthPct = traceDurationMs > 0 ? Math.max((durationMs / traceDurationMs) * 100, 0.4) : 0;
  const isError = status === 'error';

  const style = {
    '--depth': depth,
    '--svc': `var(--svc-${serviceColorIndex(service)})`,
  } as CSSProperties;

  return (
    <div
      className={cn(
        'span-row',
        isSelected && 'span-row-selected',
        isError && 'span-row-error',
        isCritical && 'span-row-crit',
      )}
      style={style}
      data-clickable={onClick ? '' : undefined}
      onClick={onClick}
      role={onClick ? 'button' : undefined}
      tabIndex={onClick ? 0 : undefined}
    >
      <div className="span-label">
        {isCritical && <span className="span-crit-tick" aria-hidden="true" />}
        <span className="span-dot" aria-hidden="true" />
        <span className="span-service">{service}</span>
        <span className="span-name">{name}</span>
      </div>
      <div className="span-track">
        <div
          className={cn('span-bar', isError && 'span-bar-error')}
          style={{ marginLeft: `${offsetPct}%`, width: `${widthPct}%` }}
        >
          {isError && (
            <span className="span-bar-flag" aria-label="error">
              !
            </span>
          )}
        </div>
      </div>
      <div
        className={cn(
          'span-duration',
          durationMs >= 1000 && 'is-slow',
          isError && 'is-error',
        )}
      >
        {durationMs < 1 ? '<1' : durationMs.toFixed(0)}
        <span className="span-duration-unit">ms</span>
      </div>
    </div>
  );
}
