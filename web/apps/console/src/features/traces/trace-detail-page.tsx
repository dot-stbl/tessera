import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { getRouteApi, Link } from '@tanstack/react-router';
import { api } from '@/shared/api/client';
import { PageTemplate } from '@/shared/ui/app-shell';
import { LogEntry, SpanDetailPanel, Waterfall, formatDuration } from '@/shared/ui/apm';
import type { WaterfallSpan } from '@/shared/ui/apm';
import { useDocumentTitle } from '@/shared/lib/use-document-title';
import type { LogEntry as LogEntryData, Span } from '@/shared/api/types';

const routeApi = getRouteApi('/traces/$traceId');

/**
 * Reconstruct the waterfall tree from VT's flat span list. Roots are spans
 * with no parent (or a parent that isn't in the set — orphans are lifted to
 * roots so nothing is dropped). `depth` is stamped on every node because the
 * Waterfall does not auto-increment it; `startOffsetMs` is relative to the
 * trace start.
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

export function TraceDetailPage() {
  const { traceId } = routeApi.useParams();
  const [selectedSpanId, setSelectedSpanId] = useState<string | undefined>(undefined);

  const traceQuery = useQuery({
    queryKey: ['trace', traceId],
    queryFn: () => api.getTrace(traceId),
  });

  const logsQuery = useQuery({
    queryKey: ['trace-logs', traceId],
    queryFn: () => api.listLogs({ traceId }),
  });

  const trace = traceQuery.data;
  useDocumentTitle(trace?.rootOperation ?? traceId);

  const tree = trace ? buildWaterfallTree(trace.spans, trace.startTime) : [];
  const selectedSpan = trace?.spans.find((s) => s.spanId === selectedSpanId);

  let subtitle = traceId;
  if (trace) {
    const dur = formatDuration(trace.durationMs);
    subtitle = `${trace.rootService} · ${dur.value}${dur.unit} · ${trace.status}`;
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
      {traceQuery.isError && (
        <div className="rounded-md border border-destructive/30 bg-destructive/10 p-4 text-sm text-destructive">
          Failed to load trace: {(traceQuery.error as Error).message}
        </div>
      )}

      {traceQuery.isLoading ? (
        <WaterfallSkeleton />
      ) : trace ? (
        <div className="flex flex-col gap-4">
          <div className="flex flex-col gap-4 lg:flex-row lg:items-start">
            <div className="min-w-0 flex-1">
              <Waterfall
                spans={tree}
                totalDurationMs={trace.durationMs}
                selectedSpanId={selectedSpanId}
                onSpanClick={(span) => setSelectedSpanId(span.id)}
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

          <LogsSection entries={logsQuery.data?.entries ?? []} isLoading={logsQuery.isLoading} />
        </div>
      ) : (
        <div className="rounded-md border border-border bg-card p-8 text-center text-sm text-muted-foreground">
          Trace not found.
        </div>
      )}
    </PageTemplate>
  );
}

function LogsSection({ entries, isLoading }: { entries: LogEntryData[]; isLoading: boolean }) {
  return (
    <section className="flex flex-col gap-2">
      <h2 className="text-xs font-medium uppercase tracking-wider text-muted-foreground">
        Logs · {entries.length}
      </h2>
      {isLoading ? (
        <SkeletonList />
      ) : entries.length === 0 ? (
        <div className="rounded-md border border-border bg-card p-8 text-center text-sm text-muted-foreground">
          No correlated logs for this trace.
        </div>
      ) : (
        <div className="overflow-hidden rounded-md border border-border bg-card">
          {entries.map((entry, i) => (
            <LogEntry
              key={`${entry.timestamp}-${i}`}
              timestamp={entry.timestamp}
              level={entry.level}
              service={entry.service}
              traceId={entry.traceId}
              spanId={entry.spanId}
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

function SkeletonList() {
  return (
    <div className="space-y-1 rounded-md border border-border bg-card p-3">
      {Array.from({ length: 4 }).map((_, i) => (
        <div key={i} className="h-7 animate-pulse rounded-sm bg-surface-2" />
      ))}
    </div>
  );
}
