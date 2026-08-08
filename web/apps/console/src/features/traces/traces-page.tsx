import { useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';
import { getRouteApi, useNavigate } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { api } from '@/shared/api';
import { PageTemplate } from '@/shared/ui/app-shell';
import { Duration, TimeFormat } from '@/shared/ui/apm';
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
  Strip,
  StripSpacer,
  Subject,
  Tag,
  TrackCell,
  WhenCell,
} from '@/shared/ui/console';
import { useDocumentTitle } from '@/shared/lib/use-document-title';
import { Button } from '@/shared/ui/primitives/button';
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
        <Blank title={t('traces.list.errorTitle')}>
          <BlankText>{(error as Error).message}</BlankText>
        </Blank>
      )}

      {isLoading ? (
        <LoadingRows />
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
    <Strip>
      {/* One segmented control rather than three loose buttons: the choice is
          exclusive, so it should look like a single thing being switched. */}
      <Seg label="Time range" value={range} options={TIME_RANGES} onChange={onRangeChange} />

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

  // The latency track is scaled to the slowest trace on screen, not to an absolute
  // ceiling: the question a list answers is "which of these is slow".
  const slowest = Math.max(...traces.map((trace) => trace.durationMs), 1);

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
          <TraceRow key={trace.traceId} trace={trace} slowestMs={slowest} />
        ))}
      </ListingBody>
    </Listing>
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
      <TrackCell share={latencyShare(trace.durationMs, slowestMs)} />
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
