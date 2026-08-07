import { type CSSProperties, useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';
import { getRouteApi, useNavigate } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { api } from '@/shared/api';
import { PageTemplate } from '@/shared/ui/app-shell';
import { Duration, TimeFormat, serviceColorIndex } from '@/shared/ui/apm';
import { useDocumentTitle } from '@/shared/lib/use-document-title';
import { Input } from '@/shared/ui/primitives/input';
import {
  DEFAULT_TIME_RANGE,
  TIME_RANGES,
  resolveTimeWindow,
  type TimeRangeKey,
} from '@/shared/lib/search-params';
import { TraceTableAria } from './trace-table-aria';
import type { TraceSummary } from '@/shared/api/types';

const routeApi = getRouteApi('/traces');

export function TracesPage() {
  const { t } = useTranslation();
  useDocumentTitle('Traces');

  // Filters live in the URL, so this view is a link someone else can open.
  const { service, range: rangeParam, table } = routeApi.useSearch();
  const range = rangeParam ?? DEFAULT_TIME_RANGE;
  const navigate = routeApi.useNavigate();
  const useAriaTable = table === 'aria';

  // A raw Date.now() lands in the query key, so every render was a cache miss:
  // the skeleton never cleared and requests fired in a loop. Quantized instead —
  // stable between renders, and still advancing on its own.
  const { startUnixMs, endUnixMs } = useMemo(() => resolveTimeWindow(range), [range]);

  const { data, isLoading, isError, error } = useQuery({
    queryKey: ['traces', { service, startUnixMs, endUnixMs }],
    queryFn: ({ signal }) => api.listTraces({ service, startUnixMs, endUnixMs, limit: 50 }, signal),
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
        useAriaTable={useAriaTable}
        onToggleTable={() =>
          void navigate({
            search: (prev) => ({ ...prev, table: useAriaTable ? undefined : 'aria' }),
            replace: true,
          })
        }
      />

      {isError && (
        <div className="blank">
          <p className="blank-title">{t('traces.list.errorTitle')}</p>
          <p className="blank-body">{(error as Error).message}</p>
        </div>
      )}

      {isLoading ? (
        <SkeletonRows />
      ) : useAriaTable ? (
        <TraceTableAria
          traces={data?.items ?? []}
          onOpenTrace={(traceId) =>
            void navigate({ to: '/traces/$traceId', params: { traceId }, search: {} })
          }
        />
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
  useAriaTable: boolean;
  onToggleTable: () => void;
}

function Toolbar({
  range,
  onRangeChange,
  service,
  onServiceChange,
  useAriaTable,
  onToggleTable,
}: ToolbarProps) {
  return (
    <div className="strip">
      {/* One segmented control rather than three loose buttons: the choice is
          exclusive, so it should look like a single thing being switched. */}
      <div className="seg" role="group" aria-label="Time range">
        {TIME_RANGES.map((key) => (
          <button
            key={key}
            type="button"
            aria-pressed={range === key}
            onClick={() => onRangeChange(key)}
          >
            {key}
          </button>
        ))}
      </div>

      {/* An active filter reads as the query it is, and clears in place. */}
      {service && (
        <span className="chip">
          <span className="chip-key">service</span>
          {service}
          <button
            type="button"
            className="chip-clear"
            aria-label="Clear service filter"
            onClick={() => onServiceChange('')}
          >
            ×
          </button>
        </span>
      )}

      <span className="strip-spacer" />

      <Input
        type="search"
        aria-label="Filter by service"
        placeholder="service…"
        value={service}
        onChange={(e) => onServiceChange(e.target.value)}
        className="h-[22px] w-40 rounded-sm px-2 font-mono text-[10.5px]"
      />

      {/* Spike switch — see trace-table-aria.tsx. Goes with the decision. */}
      <button
        type="button"
        onClick={onToggleTable}
        className="meta rounded-sm border border-border-2 bg-background px-2 py-[3px] hover:text-foreground"
        title="Compare the hand-rolled table with the React Aria build"
      >
        {useAriaTable ? 'aria' : 'plain'}
      </button>
    </div>
  );
}

function TraceTable({ traces }: { traces: TraceSummary[] }) {
  if (traces.length === 0) {
    return (
      <div className="blank">
        <p className="blank-title">Nothing in this window.</p>
        <p className="blank-body">
          Widen the range, or clear the service filter. If a service you expect is missing
          entirely, check that it is exporting to the collector.
        </p>
      </div>
    );
  }

  // The latency track is scaled to the slowest trace on screen, not to an absolute
  // ceiling: the question a list answers is "which of these is slow".
  const slowest = Math.max(...traces.map((trace) => trace.durationMs), 1);

  return (
    <table className="listing">
      <thead>
        <tr>
          <th className="col-when">When</th>
          <th>Service · Operation</th>
          <th style={{ width: 300 }}>Latency</th>
          <th style={{ width: 68 }}>Status</th>
          <th style={{ width: 92, textAlign: 'right' }}>Took</th>
          <th style={{ width: 58, textAlign: 'right' }}>Spans</th>
          <th style={{ width: 76 }}>Services</th>
        </tr>
      </thead>
      <tbody>
        {traces.map((trace) => (
          <TraceRow key={trace.traceId} trace={trace} slowestMs={slowest} />
        ))}
      </tbody>
    </table>
  );
}

/**
 * How much of the latency track a duration fills, 0–100.
 *
 * Log-scaled, not linear. Latency is log-normally distributed and one slow trace
 * sets the ceiling for the whole view: on a linear scale a single 8.9s outlier
 * squashed nine of ten rows into 2–30px stubs, so the column ranked correctly and
 * showed nothing. On a log scale the ordering survives and the differences between
 * the ordinary rows stay visible, which is what the column is for.
 */
export function latencyShare(durationMs: number, slowestMs: number): number {
  if (slowestMs <= 0) return 0;
  const share = Math.log1p(Math.max(durationMs, 0)) / Math.log1p(slowestMs);
  return Math.min(Math.max(share, 0), 1) * 100;
}

function TraceRow({ trace, slowestMs }: { trace: TraceSummary; slowestMs: number }) {
  const navigate = useNavigate();
  const open = () =>
    void navigate({ to: '/traces/$traceId', params: { traceId: trace.traceId }, search: {} });

  return (
    <tr
      data-status={trace.status}
      data-slow={trace.durationMs >= 1000 ? 'true' : undefined}
      onClick={open}
      onKeyDown={(event) => {
        if (event.key === 'Enter' || event.key === ' ') {
          event.preventDefault();
          open();
        }
      }}
      tabIndex={0}
      role="link"
      className="cursor-pointer outline-none focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-primary"
    >
      <td className="col-when">
        <TimeFormat ms={trace.startTime} relative />
      </td>
      <td>
        <span className="cell-subject">
          <span className="svc">{trace.rootService}</span>
          <span className="op">{trace.rootOperation}</span>
        </span>
      </td>
      <td
        className="cell-track"
        style={{ '--share': latencyShare(trace.durationMs, slowestMs) } as CSSProperties}
      >
        <i />
      </td>
      <td>
        <span className={`tag tag-${trace.status}`}>{trace.status}</span>
      </td>
      <td className="cell-num">
        <Duration ms={trace.durationMs} />
      </td>
      <td className="cell-num">{trace.spanCount}</td>
      <td>
        <span className="svc-dots">
          {trace.services.slice(0, 5).map((name) => (
            <i
              key={name}
              title={name}
              style={{ '--svc': `var(--svc-${serviceColorIndex(name)})` } as CSSProperties}
            />
          ))}
        </span>
      </td>
    </tr>
  );
}

function SkeletonRows() {
  // Shows the shape that is coming — gutter, rule, subject — rather than grey
  // lozenges, so nothing jumps when the rows arrive.
  return (
    <div className="loading-rows" aria-hidden="true">
      {[42, 66, 34, 58, 48, 72, 38, 54].map((w, i) => (
        <i key={i} style={{ '--w': `${w}%`, '--i': i } as CSSProperties} />
      ))}
    </div>
  );
}
