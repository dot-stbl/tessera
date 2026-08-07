import { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { getRouteApi, Link } from '@tanstack/react-router';
import { api } from '@/shared/api';
import { PageTemplate } from '@/shared/ui/app-shell';
import { LogEntry, SpanDetailPanel, Waterfall, formatDuration } from '@/shared/ui/apm';
import type { WaterfallSpan } from '@/shared/ui/apm';
import { useDocumentTitle } from '@/shared/lib/use-document-title';
import type { LogEntry as LogEntryData, Span } from '@/shared/api/types';

const routeApi = getRouteApi('/traces/$traceId');

/**
 * Reconstruct the waterfall tree from the flat span list. Roots are spans with no
 * parent (or a parent that isn't in the set — orphans are lifted to roots so
 * nothing is dropped). `depth` is stamped on every node because the Waterfall
 * does not auto-increment it; `startOffsetMs` is relative to the trace start.
 */
export function buildWaterfallTree(spans: Span[], traceStart: number): WaterfallSpan[] {
  const ids = new Set(spans.map((s) => s.spanId));
  const childrenByParent = new Map<string, Span[]>();
  const roots: Span[] = [];

  for (const span of spans) {
    const parentId = span.parentSpanId;
    if (parentId === null || !ids.has(parentId)) {
      roots.push(span);
    } else {
      const siblings = childrenByParent.get(parentId);
      if (siblings) siblings.push(span);
      else childrenByParent.set(parentId, [span]);
    }
  }

  const toNode = (span: Span, depth: number): WaterfallSpan => ({
    id: span.spanId,
    name: span.operation,
    service: span.service,
    durationMs: span.durationMs,
    startOffsetMs: span.startTime - traceStart,
    depth,
    status: span.status,
    children: (childrenByParent.get(span.spanId) ?? []).map((child) => toNode(child, depth + 1)),
  });

  return roots.map((root) => toNode(root, 0));
}

/**
 * Narrow a trace's logs to the ones emitted inside a given span.
 *
 * This is the join the product exists for, and it is exact: OpenTelemetry writes
 * `span_id` onto every log record emitted inside a span, so no time window is
 * involved. Records without a span id are never attributed to a span — they stay
 * reachable through the unfiltered trace view.
 */
export function logsForSpan(logs: LogEntryData[], spanId: string | undefined): LogEntryData[] {
  if (spanId === undefined) return logs;
  return logs.filter((entry) => entry.spanId === spanId);
}

export function TraceDetailPage() {
  const { traceId } = routeApi.useParams();
  const [selectedSpanId, setSelectedSpanId] = useState<string | undefined>(undefined);

  // One request. GetTraceResponse carries the span tree and the correlated logs
  // together, so the second /logs call this page used to make was asking for
  // something it had already been handed.
  const requestView = useQuery({
    queryKey: ['request-view', traceId],
    queryFn: ({ signal }) => api.getTrace(traceId, signal),
  });

  const view = requestView.data;
  const trace = view?.trace ?? undefined;
  // Memoized because the `?? []` fallback would otherwise allocate a fresh array
  // on every render and invalidate the log-filtering memo below each time.
  const allLogs = useMemo(() => view?.correlatedLogs ?? [], [view?.correlatedLogs]);

  useDocumentTitle(trace?.rootOperation ?? traceId);

  const tree = useMemo(
    () => (trace ? buildWaterfallTree(trace.spans, trace.startTime) : []),
    [trace],
  );

  const selectedSpan = trace?.spans.find((span) => span.spanId === selectedSpanId);
  const visibleLogs = useMemo(
    () => logsForSpan(allLogs, selectedSpanId),
    [allLogs, selectedSpanId],
  );

  let subtitle = traceId;
  if (trace) {
    const duration = formatDuration(trace.durationMs);
    subtitle = `${trace.rootService} · ${duration.value}${duration.unit} · ${trace.status}`;
    const failing = view?.erroredSpanCount ?? 0;
    if (failing > 0) {
      subtitle += ` · ${failing} failing span${failing === 1 ? '' : 's'}`;
    }
  }

  return (
    <PageTemplate
      title={trace?.rootOperation ?? 'Trace'}
      subtitle={subtitle}
      actions={
        <Link
          to="/traces"
          className="text-xs text-muted-foreground hover:text-foreground hover:underline"
        >
          ← Back to traces
        </Link>
      }
    >
      {requestView.isError && (
        <div className="rounded-md border border-destructive/30 bg-destructive/10 p-4 text-sm text-destructive">
          Failed to load trace: {(requestView.error as Error).message}
        </div>
      )}

      {requestView.isLoading ? (
        <WaterfallSkeleton />
      ) : trace ? (
        <div className="flex flex-col gap-4">
          {/* The backend answers 200 when it has spans or logs and says which, so
              a partial view is a normal state here rather than an error. */}
          {view && view.mode !== 'full' && (
            <div className="rounded-md border border-warn/40 bg-warn-soft px-3 py-2 text-xs text-warn-ink">
              Partial view —{' '}
              {view.mode === 'spansOnly'
                ? 'no logs were correlated to this trace'
                : 'no spans found; showing logs only'}
              .
            </div>
          )}

          <div className="flex flex-col gap-4 lg:flex-row lg:items-start">
            <div className="min-w-0 flex-1">
              <Waterfall
                spans={tree}
                totalDurationMs={trace.durationMs}
                selectedSpanId={selectedSpanId}
                onSpanClick={(span) =>
                  setSelectedSpanId((current) => (current === span.id ? undefined : span.id))
                }
              />
            </div>
            {selectedSpan && (
              <div className="w-full lg:w-96 lg:shrink-0">
                <SpanDetailPanel
                  service={selectedSpan.service}
                  operation={selectedSpan.operation}
                  spanId={selectedSpan.spanId}
                  durationMs={selectedSpan.durationMs}
                  status={selectedSpan.status}
                  tags={selectedSpan.tags}
                  events={selectedSpan.events.map((event) => ({
                    offsetMs: event.time - selectedSpan.startTime,
                    name: event.name,
                    attributes: event.attributes,
                  }))}
                  onClose={() => setSelectedSpanId(undefined)}
                />
              </div>
            )}
          </div>

          <LogsSection
            entries={visibleLogs}
            totalCount={allLogs.length}
            selectedSpanId={selectedSpanId}
            onClearSpanFilter={() => setSelectedSpanId(undefined)}
          />
        </div>
      ) : (
        <div className="rounded-md border border-border bg-card p-8 text-center text-sm text-muted-foreground">
          Trace not found.
        </div>
      )}
    </PageTemplate>
  );
}

interface LogsSectionProps {
  entries: LogEntryData[];
  totalCount: number;
  selectedSpanId: string | undefined;
  onClearSpanFilter: () => void;
}

function LogsSection({ entries, totalCount, selectedSpanId, onClearSpanFilter }: LogsSectionProps) {
  const scoped = selectedSpanId !== undefined;

  return (
    <section className="flex flex-col gap-2">
      <header className="flex items-center gap-3">
        <h2 className="text-xs font-medium uppercase tracking-wider text-muted-foreground">
          {scoped ? 'Logs for selected span' : 'Logs'} · {entries.length}
        </h2>
        {scoped && (
          <button
            type="button"
            onClick={onClearSpanFilter}
            className="text-xs text-muted-foreground underline hover:text-foreground"
          >
            show all {totalCount}
          </button>
        )}
      </header>

      {entries.length === 0 ? (
        <div className="rounded-md border border-border bg-card p-8 text-center text-sm text-muted-foreground">
          {scoped
            ? 'This span emitted no logs. Clear the filter to see the rest of the trace.'
            : 'No logs were correlated to this trace.'}
        </div>
      ) : (
        <div className="overflow-hidden rounded-md border border-border bg-card">
          {entries.map((entry, index) => (
            <LogEntry
              key={`${entry.timestamp}-${index}`}
              timestamp={entry.timestamp}
              level={entry.level}
              service={entry.service}
              traceId={entry.traceId ?? undefined}
              spanId={entry.spanId ?? undefined}
              message={entry.message}
              fields={entry.fields}
            />
          ))}
        </div>
      )}
    </section>
  );
}

function WaterfallSkeleton() {
  return (
    <div className="overflow-hidden rounded-md border border-border bg-card">
      <div className="space-y-2 p-4">
        {Array.from({ length: 6 }).map((_, i) => (
          <div key={i} className="h-8 animate-pulse rounded-md bg-surface-2" />
        ))}
      </div>
    </div>
  );
}
