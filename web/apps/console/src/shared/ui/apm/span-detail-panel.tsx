import { cn } from '@/shared/lib/utils';
import { StatusPill, type StatusVariant } from '@/shared/ui/primitives/status-pill';
import { Duration } from './duration';
import { TraceId } from './trace-id';

export interface SpanEvent {
  /** Offset from span start in ms (shown as e.g. "+610ms"). */
  offsetMs: number;
  /** Event name, e.g. "exception". */
  name: string;
  /** Structured attributes attached to the event. */
  attributes?: Record<string, string | number | boolean>;
}

export interface SpanDetailPanelProps {
  /** Service that produced the span. */
  service: string;
  /** Operation / span name. */
  operation: string;
  /** Span ID (rendered truncated + copyable). */
  spanId?: string;
  /** Span duration in ms. */
  durationMs: number;
  /** Span's own time, excluding children, in ms. */
  selfTimeMs?: number;
  status?: 'ok' | 'error' | 'unset';
  /** Span tags / attributes. */
  tags?: Record<string, string | number | boolean>;
  /** Span events (exceptions, checkpoints, …). */
  events?: SpanEvent[];
  /** Close handler — renders a close button when provided. */
  onClose?: () => void;
  className?: string;
}

type SpanStatus = 'ok' | 'error' | 'unset';

const STATUS_VARIANT: Record<SpanStatus, StatusVariant> = {
  ok: 'ok',
  error: 'err',
  unset: 'idle',
};

const STATUS_LABEL: Record<SpanStatus, string> = {
  ok: 'OK',
  error: 'ERROR',
  unset: 'UNSET',
};

/** True for 5xx-style status codes, which we tint as errors. */
function isServerError(key: string, value: string | number | boolean): boolean {
  return key === 'http.status_code' && String(value).startsWith('5');
}

/**
 * Span detail side panel — the flyout shown when a span is selected in the
 * waterfall. Header (status + service·operation + id), timing (duration +
 * self time), tags, and events (exceptions highlighted). Presentational: drop
 * it into a Sheet/Drawer or an inline column.
 *
 * Usage:
 *   <SpanDetailPanel
 *     service="stripe"
 *     operation="api.call POST /v1/charges"
 *     durationMs={880}
 *     selfTimeMs={610}
 *     status="error"
 *     tags={{ 'http.status_code': '502' }}
 *     events={[{ offsetMs: 610, name: 'exception' }]}
 *     onClose={() => setSelected(undefined)}
 *   />
 */
export function SpanDetailPanel({
  service,
  operation,
  spanId,
  durationMs,
  selfTimeMs,
  status = 'unset',
  tags,
  events,
  onClose,
  className,
}: SpanDetailPanelProps) {
  const tagEntries = tags ? Object.entries(tags) : [];

  return (
    <aside
      data-slot="span-detail-panel"
      aria-label={`Span detail: ${service} ${operation}`}
      className={cn(
        'flex w-full flex-col gap-4 rounded-lg border border-border bg-card p-4 text-foreground',
        className,
      )}
    >
      <header className="flex items-start gap-2">
        <div className="flex min-w-0 flex-col gap-1.5">
          <div className="flex items-center gap-2">
            <StatusPill variant={STATUS_VARIANT[status]} size="sm">
              {STATUS_LABEL[status]}
            </StatusPill>
            {spanId && <TraceId id={spanId} />}
          </div>
          <h2 className="truncate font-sans text-sm font-semibold">
            <span className="font-normal text-muted-foreground">{service} · </span>
            {operation}
          </h2>
        </div>
        {onClose && (
          <button
            type="button"
            onClick={onClose}
            aria-label="Close span detail"
            className="ml-auto rounded-sm p-1 text-muted-foreground transition-colors hover:bg-muted hover:text-foreground"
          >
            ✕
          </button>
        )}
      </header>

      <dl className="flex flex-wrap gap-x-6 gap-y-1 font-mono text-[11px]">
        <div className="flex items-baseline gap-2">
          <dt className="text-muted-foreground">duration</dt>
          <dd>
            <Duration ms={durationMs} />
          </dd>
        </div>
        {selfTimeMs != null && (
          <div className="flex items-baseline gap-2">
            <dt className="text-muted-foreground">self</dt>
            <dd>
              <Duration ms={selfTimeMs} />
            </dd>
          </div>
        )}
      </dl>

      {tagEntries.length > 0 && (
        <section className="flex flex-col gap-1.5">
          <h3 className="text-[10px] font-semibold tracking-wide text-muted-foreground uppercase">
            Attributes
          </h3>
          <dl className="grid grid-cols-[max-content_1fr] gap-x-4 gap-y-1 font-mono text-[11px]">
            {tagEntries.map(([key, value]) => (
              <div key={key} className="contents">
                <dt className="text-muted-foreground">{key}</dt>
                <dd
                  className={cn(
                    'break-all',
                    isServerError(key, value) && 'font-semibold text-err-ink',
                  )}
                >
                  {String(value)}
                </dd>
              </div>
            ))}
          </dl>
        </section>
      )}

      {events && events.length > 0 && (
        <section className="flex flex-col gap-1.5">
          <h3 className="text-[10px] font-semibold tracking-wide text-muted-foreground uppercase">
            Events
          </h3>
          <ul className="flex flex-col gap-2">
            {events.map((event, i) => {
              const isException = event.name.toLowerCase() === 'exception';
              const attrs = event.attributes ? Object.entries(event.attributes) : [];
              return (
                <li key={`${event.name}-${i}`} className="flex flex-col gap-1 font-mono text-[11px]">
                  <div className="flex items-baseline gap-2">
                    <span className="tabular-nums text-muted-foreground">+{event.offsetMs}ms</span>
                    <span className={cn('font-semibold', isException ? 'text-err-ink' : 'text-foreground')}>
                      {event.name}
                    </span>
                  </div>
                  {attrs.length > 0 && (
                    <dl className="grid grid-cols-[max-content_1fr] gap-x-4 gap-y-0.5 pl-4">
                      {attrs.map(([k, v]) => (
                        <div key={k} className="contents">
                          <dt className="text-muted-foreground">{k}</dt>
                          <dd className="break-all">{String(v)}</dd>
                        </div>
                      ))}
                    </dl>
                  )}
                </li>
              );
            })}
          </ul>
        </section>
      )}
    </aside>
  );
}
