import { useQuery } from '@tanstack/react-query';
import { getRouteApi, Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { api } from '@/shared/api';
import { PageTemplate } from '@/shared/ui/app-shell';
import { TimeFormat } from '@/shared/ui/apm';
import { Duration } from '@/shared/ui/apm';
import { useDocumentTitle } from '@/shared/lib/use-document-title';
import { useMemo } from 'react';
import {
  DEFAULT_TIME_RANGE,
  TIME_RANGES,
  resolveTimeWindow,
  type TimeRangeKey,
} from '@/shared/lib/search-params';
import { cn } from '@/shared/lib/utils';
import type { TraceSummary, TraceStatus } from '@/shared/api/types';

const routeApi = getRouteApi('/traces');

export function TracesPage() {
  const { t } = useTranslation();
  useDocumentTitle('Traces');

  // Filters live in the URL, so this view is a link someone else can open.
  const { service, range: rangeParam } = routeApi.useSearch();
  const range = rangeParam ?? DEFAULT_TIME_RANGE;
  const navigate = routeApi.useNavigate();

  // A raw Date.now() lands in the query key, so every render was a cache miss:
  // the skeleton never cleared and requests fired in a loop. Quantized instead —
  // stable between renders, and still advancing on its own.
  const { startUnixMs, endUnixMs } = useMemo(() => resolveTimeWindow(range), [range]);

  const { data, isLoading, isError, error } = useQuery({
    queryKey: ['traces', { service, startUnixMs, endUnixMs }],
    queryFn: ({ signal }) =>
      api.listTraces({ service, startUnixMs, endUnixMs, limit: 50 }, signal),
    refetchInterval: 30_000,
  });

  return (
    <PageTemplate
      title={t('traces.list.title')}
      subtitle={`${data?.items.length ?? 0} traces · last ${range}`}
    >
      <Toolbar
        range={range}
        onRangeChange={(next) =>
          void navigate({ search: (prev) => ({ ...prev, range: next }), replace: true })
        }
        service={service ?? ''}
        onServiceChange={(next) =>
          void navigate({
            search: (prev) => ({ ...prev, service: next === '' ? undefined : next }),
            replace: true,
          })
        }
      />

      {isError && (
        <div className="rounded-md border border-destructive/30 bg-destructive/10 p-4 text-sm text-destructive">
          {t('traces.list.errorTitle')}: {(error as Error).message}
        </div>
      )}

      {isLoading ? (
        <SkeletonTable />
      ) : (
        <TraceTable traces={data?.items ?? []} />
      )}
    </PageTemplate>
  );
}

interface ToolbarProps {
  range: TimeRangeKey;
  onRangeChange: (next: TimeRangeKey) => void;
  service: string;
  onServiceChange: (next: string) => void;
}

function Toolbar({ range, onRangeChange, service, onServiceChange }: ToolbarProps) {
  return (
    <div className="flex flex-wrap items-center gap-2 rounded-md border border-border bg-card p-3">
      <div className="flex items-center gap-1">
        {TIME_RANGES.map((r) => (
          <button
            key={r}
            onClick={() => onRangeChange(r)}
            className={cn(
              'rounded-sm border px-2 py-1 text-xs',
              range === r
                ? 'border-foreground/60 bg-accent text-accent-foreground'
                : 'border-border bg-background text-muted-foreground hover:border-foreground/30',
            )}
          >
            {r === '1h' ? 'Last 1h' : r === '6h' ? 'Last 6h' : 'Last 24h'}
          </button>
        ))}
      </div>
      <div className="ml-auto">
        <input
          type="text"
          placeholder="Filter by service..."
          value={service}
          onChange={(e) => onServiceChange(e.target.value)}
          className="h-7 w-48 rounded-sm border border-border bg-background px-2 text-xs text-foreground placeholder:text-muted-foreground focus:border-foreground/60 focus:outline-none"
        />
      </div>
    </div>
  );
}

function TraceTable({ traces }: { traces: TraceSummary[] }) {
  if (traces.length === 0) {
    return (
      <div className="rounded-md border border-border bg-card p-8 text-center text-sm text-muted-foreground">
        No traces found in the selected time range.
      </div>
    );
  }

  return (
    <div className="overflow-hidden rounded-md border border-border bg-card">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b border-border bg-surface-3 text-left text-xs font-medium uppercase tracking-wider text-muted-foreground">
            <th className="px-4 py-2">Service · Operation</th>
            <th className="px-4 py-2">Status</th>
            <th className="px-4 py-2 text-right">Duration</th>
            <th className="px-4 py-2 text-right">Spans</th>
            <th className="px-4 py-2 text-right">Started</th>
          </tr>
        </thead>
        <tbody>
          {traces.map((trace) => (
            <TraceRow key={trace.traceId} trace={trace} />
          ))}
        </tbody>
      </table>
    </div>
  );
}

function TraceRow({ trace }: { trace: TraceSummary }) {
  return (
    <tr className="border-b border-border last:border-b-0 hover:bg-surface-2">
      <td className="px-4 py-2">
        <Link
          to="/traces/$traceId"
          params={{ traceId: trace.traceId }}
          search={{}}
          className="block hover:underline"
        >
          <div className="font-medium text-foreground">{trace.rootService}</div>
          <div className="text-xs text-muted-foreground">{trace.rootOperation}</div>
        </Link>
      </td>
      <td className="px-4 py-2">
        <StatusPill status={trace.status} />
      </td>
      <td className="px-4 py-2 text-right">
        <Duration ms={trace.durationMs} />
      </td>
      <td className="px-4 py-2 text-right text-muted-foreground">{trace.spanCount}</td>
      <td className="px-4 py-2 text-right">
        <TimeFormat ms={trace.startTime} relative />
      </td>
    </tr>
  );
}

function StatusPill({ status }: { status: TraceStatus }) {
  const className =
    status === 'error'
      ? 'bg-err-soft text-err-ink'
      : status === 'unset'
        ? 'bg-idle-soft text-idle-ink'
        : 'bg-ok-soft text-ok-ink';
  return (
    <span className={cn('pill', className)}>
      <span className="dot" />
      {status}
    </span>
  );
}

function SkeletonTable() {
  return (
    <div className="overflow-hidden rounded-md border border-border bg-card">
      <div className="space-y-2 p-4">
        {Array.from({ length: 5 }).map((_, i) => (
          <div key={i} className="h-10 animate-pulse rounded-md bg-surface-2" />
        ))}
      </div>
    </div>
  );
}