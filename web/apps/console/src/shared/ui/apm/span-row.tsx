import { type CSSProperties, type KeyboardEvent, type ReactNode } from 'react';

/**
 * A log emitted inside this span, drawn on its track. Lets the waterfall answer
 * "when in this span did it start going wrong" without leaving the timeline —
 * the backend already computes the offsets.
 */
export interface SpanRowMarker {
  /** Offset from trace start, ms — same origin as `startOffsetMs`. */
  offsetMs: number;
  level: 'trace' | 'debug' | 'info' | 'warn' | 'error' | 'fatal';
  message: string;
}
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
  /** Warn/error logs emitted inside this span, drawn as ticks on the track. */
  markers?: SpanRowMarker[];
  children?: ReactNode;
}

/**
 * Offset from the trace start, for the time gutter. Kept short enough for a 62px
 * column: sub-second offsets in ms, the rest in seconds with one decimal. The
 * root reads `0` rather than `+0ms`, because the root *is* the origin.
 */
export function formatOffset(offsetMs: number): string {
  if (offsetMs <= 0) return '0';
  if (offsetMs < 1000) return `+${Math.round(offsetMs)}ms`;
  return `+${(offsetMs / 1000).toFixed(1)}s`;
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
  markers,
}: SpanRowProps) {
  const offsetPct = traceDurationMs > 0 ? (startOffsetMs / traceDurationMs) * 100 : 0;
  const widthPct = traceDurationMs > 0 ? Math.max((durationMs / traceDurationMs) * 100, 0.4) : 0;
  const isError = status === 'error';

  const style = {
    '--depth': depth,
    '--svc': `var(--svc-${serviceColorIndex(service)})`,
  } as CSSProperties;

  // The row already advertised role="button" and took focus, then did nothing
  // when a key was pressed. Enter and Space are what that contract promises.
  function handleKeyDown(event: KeyboardEvent<HTMLDivElement>) {
    if (!onClick) return;
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      onClick();
    }
  }

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
      onKeyDown={onClick ? handleKeyDown : undefined}
      role={onClick ? 'button' : undefined}
      tabIndex={onClick ? 0 : undefined}
      aria-pressed={onClick ? isSelected : undefined}
    >
      {/* The time gutter: this span's offset from the trace start. Same column,
          same width, same face as the "when" column in every other listing, so
          "where in time am I" is answered in one place across the app. */}
      <div className="span-offset">{formatOffset(startOffsetMs)}</div>
      <div className="span-label">
        <span className="span-dot" aria-hidden="true" />
        <span className="span-service">{service}</span>
        <span className="span-name">{name}</span>
      </div>
      <div className="span-track">
        <div
          className={cn('span-bar', isError && 'span-bar-error')}
          style={{ marginLeft: `${offsetPct}%`, width: `${widthPct}%` }}
        />
        {/* Markers sit on the track, not the bar, so their offsets stay in
            trace coordinates rather than the span's own width. */}
        {markers?.map((marker) => (
          <span
            key={`${marker.offsetMs}-${marker.message}`}
            className={cn('span-marker', `span-marker-${marker.level}`)}
            style={{
              left: `${traceDurationMs > 0 ? (marker.offsetMs / traceDurationMs) * 100 : 0}%`,
            }}
            title={`${marker.level.toUpperCase()} · ${marker.message}`}
          />
        ))}
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
