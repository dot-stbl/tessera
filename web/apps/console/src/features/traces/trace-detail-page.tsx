import { useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';
import { getRouteApi, Link } from '@tanstack/react-router';
import { api } from '@/shared/api';
import { PageTemplate } from '@/shared/ui/app-shell';
import { LogEntry, SpanDetailPanel, Waterfall, formatDuration } from '@/shared/ui/apm';
import type { SpanRowMarker, WaterfallSpan } from '@/shared/ui/apm';
import {
  Blank,
  BlankText,
  LoadingRows,
  Meta,
  Notice,
  Strip,
} from '@/shared/ui/console';
import { Button } from '@/shared/ui/primitives/button';
import { useDocumentTitle } from '@/shared/lib/use-document-title';
import type {
  LogEntry as LogEntryData,
  LogMarker,
  RequestViewMode,
  Span,
} from '@/shared/api/types';

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

/**
 * Group timeline markers by the span they belong to.
 *
 * Markers without a span id are dropped rather than parked on the root: a log
 * emitted outside any span has no position in the span tree, and inventing one
 * would put a marker on a bar it never belonged to. They stay visible in the
 * unfiltered log list below the waterfall.
 */
export function groupMarkersBySpan(markers: LogMarker[]): Map<string, SpanRowMarker[]> {
  const grouped = new Map<string, SpanRowMarker[]>();

  for (const marker of markers) {
    if (marker.spanId === null) continue;
    const forSpan = grouped.get(marker.spanId);
    const entry: SpanRowMarker = {
      offsetMs: marker.offsetMs,
      level: marker.level,
      message: marker.message,
    };
    if (forSpan) forSpan.push(entry);
    else grouped.set(marker.spanId, [entry]);
  }

  return grouped;
}

export function TraceDetailPage() {
  const { traceId } = routeApi.useParams();
  // The selected span is URL state: the link you hand to someone else opens on
  // the span you were reading, with its logs already narrowed.
  const { span: selectedSpanId } = routeApi.useSearch();
  const navigate = routeApi.useNavigate();

  const selectSpan = (spanId: string | undefined) =>
    void navigate({ search: (prev) => ({ ...prev, span: spanId }), replace: true });

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
  const markersBySpanId = useMemo(() => groupMarkersBySpan(view?.markers ?? []), [view?.markers]);

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
        <Blank title="Could not load this trace.">
          <BlankText>{(requestView.error as Error).message}</BlankText>
        </Blank>
      )}

      {requestView.isLoading ? (
        <LoadingRows count={6} />
      ) : view ? (
        <div className="flex flex-col">
          {/* Backend 200 + mode when spans or logs exist; empty only when both miss. */}
          {view.mode !== 'full' && <Notice>Partial view — {modeBanner(view.mode)}.</Notice>}

          {view.dependencyGraph && view.dependencyGraph.edges.length > 0 && (
            <Strip>
              <Meta>
                Dependencies · {view.dependencyGraph.nodes.length} nodes ·{' '}
                {view.dependencyGraph.edges.length} edges
                {view.dependencyGraph.edges.some((edge) => edge.errorCount > 0)
                  ? ` · ${view.dependencyGraph.edges.reduce((sum, edge) => sum + edge.errorCount, 0)} client errors`
                  : ''}
              </Meta>
            </Strip>
          )}

          {trace ? (
            <div className="flex flex-col border-b border-border-2 bg-card lg:flex-row lg:items-stretch">
              <div className="min-w-0 flex-1">
                <Waterfall
                  spans={tree}
                  totalDurationMs={trace.durationMs}
                  selectedSpanId={selectedSpanId}
                  markersBySpanId={markersBySpanId}
                  onSpanClick={(span) =>
                    selectSpan(span.id === selectedSpanId ? undefined : span.id)
                  }
                />
              </div>
              {selectedSpan && (
                <div className="w-full lg:w-[340px] lg:shrink-0">
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
                    onClose={() => selectSpan(undefined)}
                  />
                </div>
              )}
            </div>
          ) : null}

          <LogsSection
            entries={visibleLogs}
            totalCount={allLogs.length}
            selectedSpanId={selectedSpanId}
            onClearSpanFilter={() => selectSpan(undefined)}
          />
        </div>
      ) : (
        <Blank title={`No trace with id ${traceId}.`}>
          <BlankText>
            Either it fell outside the retention window, or the id belongs to a different tenant.
            Check the id against the alert it came from.
          </BlankText>
        </Blank>
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
    <div className="flex flex-col">
      <Strip>
        <Meta>
          {scoped ? 'Logs for selected span' : 'Logs'} · {entries.length}
        </Meta>
        {scoped && (
          <Button variant="link" className="h-auto p-0 text-[11px]" onClick={onClearSpanFilter}>
            show all {totalCount}
          </Button>
        )}
      </Strip>

      {entries.length === 0 ? (
        <Blank
          title={scoped ? 'This span emitted no logs.' : 'No logs were correlated to this trace.'}
        >
          <BlankText>
            {scoped
              ? 'Clear the span filter to read the rest of the trace — the failure is often logged by the caller, not by the span that failed.'
              : 'Logs correlate by span id, which the SDK writes automatically inside a span. A service that logs outside its spans will not appear here.'}
          </BlankText>
        </Blank>
      ) : (
        <div className="log-stream">
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
    </div>
  );
}

function modeBanner(mode: RequestViewMode): string {
  switch (mode) {
    case 'spansOnly':
      return 'no logs were correlated to this trace';
    case 'logsOnly':
      return 'no spans found; showing logs only';
    case 'empty':
      return 'neither spans nor logs were found';
    default:
      return mode;
  }
}

