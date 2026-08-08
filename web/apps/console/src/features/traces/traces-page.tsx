import { useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';
import { getRouteApi, useNavigate } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { api } from '@/shared/api';
import { PageTemplate } from '@/shared/ui/app-shell';
import { Autorenew, Hub, Speed, Timeline, Warning } from '@nine-thirty-five/material-symbols-react/rounded/400';
import { Duration, TimeFormat, formatDuration } from '@/shared/ui/apm';
import {
  Blank,
  BlankText,
  Cell,
  Chip,
  Column,
  Listing,
  ListingBody,
  ListingHead,
  LoadingRows,
  NumCell,
  Row,
  Seg,
  ServiceDots,
  StatBar,
  Strip,
  StripSpacer,
  Subject,
  Tag,
  TrackCell,
  WhenCell,
} from '@/shared/ui/console';
import { useDocumentTitle } from '@/shared/lib/use-document-title';
import { cn } from '@/shared/lib/utils';
import { Button } from '@/shared/ui/primitives/button';
import { Input } from '@/shared/ui/primitives/input';
import {
  DEFAULT_TIME_RANGE,
  SLOW_THRESHOLD_MS,
  TIME_RANGES,
  TRACE_STATUS_FILTERS,
  resolveTimeWindow,
  type TimeRangeKey,
  type TraceStatusFilter,
} from '@/shared/lib/search-params';
import { TraceTableAria } from './trace-table-aria';
import type { TraceSummary } from '@/shared/api/types';

const routeApi = getRouteApi('/traces');

export function TracesPage() {
  const { t } = useTranslation();
  useDocumentTitle('Traces');

  // Filters live in the URL, so this view is a link someone else can open.
  const { service, range: rangeParam, table, status: statusParam } = routeApi.useSearch();
  const range = rangeParam ?? DEFAULT_TIME_RANGE;
  const status = statusParam ?? 'all';
  const navigate = routeApi.useNavigate();
  const useAriaTable = table === 'aria';

  // A raw Date.now() lands in the query key, so every render was a cache miss:
  // the skeleton never cleared and requests fired in a loop. Quantized instead —
  // stable between renders, and still advancing on its own.
  const { startUnixMs, endUnixMs } = useMemo(() => resolveTimeWindow(range), [range]);

  const { data, isLoading, isError, error, isFetching, refetch } = useQuery({
    queryKey: ['traces', { service, startUnixMs, endUnixMs }],
    queryFn: ({ signal }) => api.listTraces({ service, startUnixMs, endUnixMs, limit: 500 }, signal),
    refetchInterval: 30_000,
  });

  // Status narrows client-side. The window is already in hand, so a round trip
  // to drop rows would only add latency to a filter that has to feel instant.
  const visible = useMemo(() => {
    const items = data?.items ?? [];
    if (status === 'errors') return items.filter((trace) => trace.status === 'error');
    if (status === 'slow') return items.filter((trace) => trace.durationMs >= SLOW_THRESHOLD_MS);
    return items;
  }, [data?.items, status]);

  return (
    <PageTemplate
      title={t('traces.list.title')}
      subtitle={`${visible.length.toLocaleString()} of ${(data?.items.length ?? 0).toLocaleString()} · last ${range}`}
      actions={
        <Button
          variant="outline"
          className="h-[26px] gap-1.5 px-2.5 text-[11.5px]"
          onClick={() => void refetch()}
          disabled={isFetching}
        >
          <Autorenew className={cn('size-3.5', isFetching && 'animate-spin')} />
          {isFetching ? 'Refreshing' : 'Refresh'}
        </Button>
      }
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
        status={status}
        onStatusChange={(next) =>
          void navigate({
            search: (prev) => ({ ...prev, status: next === 'all' ? undefined : next }),
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
        <Blank title={t('traces.list.errorTitle')}>
          <BlankText>{(error as Error).message}</BlankText>
        </Blank>
      )}

      {!isLoading && !isError && <TraceStats traces={visible} range={range} />}

      {isLoading ? (
        <LoadingRows count={12} />
      ) : useAriaTable ? (
        <TraceTableAria
          traces={visible}
          onOpenTrace={(traceId) =>
            void navigate({ to: '/traces/$traceId', params: { traceId }, search: {} })
          }
        />
      ) : (
        <TraceTable traces={visible} />
      )}
    </PageTemplate>
  );
}

/**
 * The four numbers that decide whether the rows below are worth reading. An
 * operator arriving from an alert asks "is this window bad" before "which row" —
 * without this they scan forty rows to answer it themselves.
 */
function TraceStats({ traces, range }: { traces: TraceSummary[]; range: TimeRangeKey }) {
  const failed = traces.filter((trace) => trace.status === 'error');
  const rate = traces.length > 0 ? (failed.length / traces.length) * 100 : 0;
  const sorted = [...traces].map((trace) => trace.durationMs).sort((a, b) => a - b);
  const p95 = sorted[Math.min(sorted.length - 1, Math.floor(sorted.length * 0.95))] ?? 0;

  // Which service roots the most failures — the one to look at first, and the
  // reason the error count alone is not enough.
  const blame = new Map<string, number>();
  for (const trace of failed) blame.set(trace.rootService, (blame.get(trace.rootService) ?? 0) + 1);
  const worst = [...blame.entries()].sort((a, b) => b[1] - a[1])[0];

  return (
    <StatBar
      items={[
        {
          icon: Timeline,
          label: 'traces',
          value: traces.length.toLocaleString(),
          sub: `in the last ${range}`,
        },
        {
          icon: Warning,
          label: 'failing',
          value: `${rate < 10 ? rate.toFixed(1) : Math.round(rate)}%`,
          bad: rate >= 1,
          sub: `${failed.length} of ${traces.length}`,
        },
        {
          icon: Speed,
          label: 'p95',
          value: <Duration ms={p95} />,
          sub: sorted.length > 0 ? `slowest ${formatDuration(sorted.at(-1) ?? 0).value}${formatDuration(sorted.at(-1) ?? 0).unit}` : '—',
        },
        {
          icon: Hub,
          label: 'blamed',
          value: worst?.[0] ?? '—',
          sub: worst ? `${worst[1]} failing traces rooted here` : 'nothing failing',
        },
      ]}
    />
  );
}

interface ToolbarProps {
  range: TimeRangeKey;
  onRangeChange: (next: TimeRangeKey) => void;
  service: string;
  onServiceChange: (next: string) => void;
  status: TraceStatusFilter;
  onStatusChange: (next: TraceStatusFilter) => void;
  useAriaTable: boolean;
  onToggleTable: () => void;
}

function Toolbar({
  range,
  onRangeChange,
  service,
  onServiceChange,
  status,
  onStatusChange,
  useAriaTable,
  onToggleTable,
}: ToolbarProps) {
  return (
    <Strip>
      {/* One segmented control rather than three loose buttons: the choice is
          exclusive, so it should look like a single thing being switched. */}
      <Seg label="Time range" value={range} options={TIME_RANGES} onChange={onRangeChange} />
      <Seg
        label="Status"
        value={status}
        options={TRACE_STATUS_FILTERS}
        onChange={onStatusChange}
        format={(key) => (key === 'slow' ? '≥1s' : key)}
      />

      {/* An active filter reads as the query it is, and clears in place. */}
      {service && (
        <Chip
          name="service"
          value={service}
          clearLabel="Clear service filter"
          onClear={() => onServiceChange('')}
        />
      )}

      <StripSpacer />

      <Input
        type="search"
        aria-label="Filter by service"
        placeholder="service…"
        value={service}
        onChange={(e) => onServiceChange(e.target.value)}
        className="h-[22px] w-40 rounded-sm px-2 font-mono text-[10.5px]"
      />

      {/* Spike switch — see trace-table-aria.tsx. Goes with the decision. */}
      <Button
        variant="outline"
        onClick={onToggleTable}
        className="meta h-[22px] rounded-sm px-2"
        title="Compare the hand-rolled table with the React Aria build"
      >
        {useAriaTable ? 'aria' : 'plain'}
      </Button>
    </Strip>
  );
}

function TraceTable({ traces }: { traces: TraceSummary[] }) {
  if (traces.length === 0) {
    return (
      <Blank title="Nothing in this window.">
        <BlankText>
          Widen the range, or clear the service filter. If a service you expect is missing
          entirely, check that it is exporting to the collector.
        </BlankText>
      </Blank>
    );
  }

  // The track is scaled to the window on screen, not to an absolute ceiling: the
  // question a list answers is "which of these is slow", which is relative by
  // definition. Both ends are needed — see latencyShare.
  const durations = traces.map((trace) => trace.durationMs);
  const slowest = Math.max(...durations, 1);
  const fastest = Math.min(...durations, slowest);

  return (
    <Listing>
      <ListingHead>
        <Column when>When</Column>
        <Column>Service · Operation</Column>
        <Column width={300}>Latency</Column>
        <Column width={68}>Status</Column>
        <Column width={92} align="right">
          Took
        </Column>
        <Column width={58} align="right">
          Spans
        </Column>
        <Column width={76}>Services</Column>
      </ListingHead>
      <ListingBody>
        {traces.map((trace) => (
          <TraceRow
            key={trace.traceId}
            trace={trace}
            slowestMs={slowest}
            fastestMs={fastest}
          />
        ))}
      </ListingBody>
    </Listing>
  );
}

/**
 * How much of the latency track a duration fills, 0–100.
 *
 * Log-scaled *and* normalized to the window on screen — both halves matter, and
 * each was learned the hard way:
 *
 *  - Linear against the slowest row: one 8.9s outlier squashed nine of ten rows
 *    into 2–30px stubs. The column ranked correctly and showed nothing.
 *  - Log against zero: with forty rows between 40ms and 1.2s, log1p(198)/log1p(1200)
 *    is 0.75 — every bar sat between 70% and 100% and the column again showed
 *    nothing, this time by being uniformly full.
 *
 * Anchoring the low end at the fastest row on screen spends the whole track on
 * the spread that actually exists. The floor keeps the fastest row visible as a
 * bar rather than as an empty cell.
 */
export function latencyShare(durationMs: number, slowestMs: number, fastestMs = 0): number {
  if (slowestMs <= 0) return 0;
  const top = Math.log1p(slowestMs);
  const floor = Math.log1p(Math.max(fastestMs, 0));
  const span = top - floor;
  // Degenerate window — every row the same duration. Half-full is honest: there
  // is nothing to rank, and a full bar would imply there was.
  if (span <= 0.0001) return 50;

  const share = (Math.log1p(Math.max(durationMs, 0)) - floor) / span;
  return Math.min(Math.max(share, 0), 1) * 92 + 8;
}

function TraceRow({
  trace,
  slowestMs,
  fastestMs,
}: {
  trace: TraceSummary;
  slowestMs: number;
  fastestMs: number;
}) {
  const navigate = useNavigate();

  return (
    <Row
      status={trace.status}
      slow={trace.durationMs >= 1000}
      onOpen={() =>
        void navigate({ to: '/traces/$traceId', params: { traceId: trace.traceId }, search: {} })
      }
    >
      <WhenCell>
        <TimeFormat ms={trace.startTime} relative />
      </WhenCell>
      <Cell>
        <Subject service={trace.rootService} operation={trace.rootOperation} />
      </Cell>
      <TrackCell share={latencyShare(trace.durationMs, slowestMs, fastestMs)} />
      <Cell>
        <Tag tone={trace.status}>{trace.status}</Tag>
      </Cell>
      <NumCell>
        <Duration ms={trace.durationMs} />
      </NumCell>
      <NumCell>{trace.spanCount}</NumCell>
      <Cell>
        <ServiceDots names={trace.services} />
      </Cell>
    </Row>
  );
}
