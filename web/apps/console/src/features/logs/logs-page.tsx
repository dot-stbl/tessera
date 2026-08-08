import { useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';
import { getRouteApi } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { api } from '@/shared/api';
import { PageTemplate } from '@/shared/ui/app-shell';
import { Description, Error as ErrorGlyph, Hub, Warning } from '@nine-thirty-five/material-symbols-react/rounded/400';
import { LogEntry } from '@/shared/ui/apm';
import {
  Blank,
  BlankText,
  Chip,
  LoadingRows,
  Seg,
  StatBar,
  Strip,
  StripSpacer,
} from '@/shared/ui/console';
import { useDocumentTitle } from '@/shared/lib/use-document-title';
import { Input } from '@/shared/ui/primitives/input';
import {
  DEFAULT_TIME_RANGE,
  TIME_RANGES,
  resolveTimeWindow,
  type TimeRangeKey,
} from '@/shared/lib/search-params';
import type { LogEntry as LogEntryData } from '@/shared/api/types';

const routeApi = getRouteApi('/logs');

export function LogsPage() {
  const { t } = useTranslation();
  useDocumentTitle('Logs');

  // Same rule as the trace explorer: what you are looking at is in the URL, so
  // "logs for this trace id" is a link rather than a set of typed-in filters.
  const { stream, traceId, range: rangeParam } = routeApi.useSearch();
  const range = rangeParam ?? DEFAULT_TIME_RANGE;
  const navigate = routeApi.useNavigate();

  const setSearch = (patch: { stream?: string; traceId?: string; range?: TimeRangeKey }) =>
    void navigate({ search: (prev) => ({ ...prev, ...patch }), replace: true });

  const { startUnixMs, endUnixMs } = useMemo(() => resolveTimeWindow(range), [range]);

  const { data, isLoading, isError, error } = useQuery({
    queryKey: ['logs', { stream, traceId, startUnixMs, endUnixMs }],
    queryFn: ({ signal }) =>
      api.listLogs(
        {
          // The backend narrows logs by trace id and stream. There is no
          // free-text parameter yet — ad-hoc LogsQL search is a later phase — so
          // the toolbar filters by service stream instead of pretending.
          stream,
          traceId,
          startUnixMs,
          endUnixMs,
          limit: 500,
        },
        signal,
      ),
    refetchInterval: 30_000,
  });

  return (
    <PageTemplate
      title={t('logs.title')}
      subtitle={`${data?.items.length ?? 0} log entries · last ${range}`}
    >
      <Toolbar
        range={range}
        onRangeChange={(next) => setSearch({ range: next })}
        query={stream ?? ''}
        onQueryChange={(next) => setSearch({ stream: next === '' ? undefined : next })}
        traceId={traceId ?? ''}
        onTraceIdChange={(next) => setSearch({ traceId: next === '' ? undefined : next })}
      />

      {isError && (
        <Blank title="Could not read the log stream.">
          <BlankText>{(error as Error).message}</BlankText>
        </Blank>
      )}

      {!isLoading && !isError && data && data.items.length > 0 && (
        <LogStats entries={data.items} range={range} />
      )}

      {isLoading ? (
        <LoadingRows count={12} />
      ) : data && data.items.length === 0 ? (
        <Blank title="No log entries in this window.">
          <BlankText>
            Widen the range, or clear the service and trace filters. A trace id filter shows only
            what that request emitted, which is often nothing.
          </BlankText>
        </Blank>
      ) : (
        <div className="log-stream">
          {data?.items.map((entry, i) => (
            <LogEntry
              key={`${entry.timestamp}-${i}`}
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
    </PageTemplate>
  );
}

/**
 * What the window contains before you start reading it. The top emitter matters
 * as much as the counts: a stream that is 80% one service is usually that
 * service having a bad time, not the whole platform.
 */
function LogStats({ entries, range }: { entries: LogEntryData[]; range: TimeRangeKey }) {
  const errors = entries.filter((entry) => entry.level === 'error' || entry.level === 'fatal');
  const warnings = entries.filter((entry) => entry.level === 'warn');

  const byService = new Map<string, number>();
  for (const entry of entries) byService.set(entry.service, (byService.get(entry.service) ?? 0) + 1);
  const top = [...byService.entries()].sort((a, b) => b[1] - a[1])[0];

  return (
    <StatBar
      items={[
        {
          icon: Description,
          label: 'entries',
          value: entries.length.toLocaleString(),
          sub: `in the last ${range}`,
        },
        {
          icon: ErrorGlyph,
          label: 'errors',
          value: errors.length.toLocaleString(),
          bad: errors.length > 0,
          sub: `${new Set(errors.map((entry) => entry.service)).size} services affected`,
        },
        {
          icon: Warning,
          label: 'warnings',
          value: warnings.length.toLocaleString(),
          sub: warnings.length > 0 ? 'retries and degraded paths' : 'nothing degraded',
        },
        {
          icon: Hub,
          label: 'loudest',
          value: top?.[0] ?? '—',
          sub: top ? `${Math.round((top[1] / entries.length) * 100)}% of the stream` : '—',
        },
      ]}
    />
  );
}

interface ToolbarProps {
  range: TimeRangeKey;
  onRangeChange: (next: TimeRangeKey) => void;
  query: string;
  onQueryChange: (next: string) => void;
  traceId: string;
  onTraceIdChange: (next: string) => void;
}

function Toolbar({
  range,
  onRangeChange,
  query,
  onQueryChange,
  traceId,
  onTraceIdChange,
}: ToolbarProps) {
  return (
    <Strip>
      <Seg label="Time range" value={range} options={TIME_RANGES} onChange={onRangeChange} />

      {/* Active filters read as the query they are and clear in place. */}
      {query && (
        <Chip
          name="service"
          value={query}
          clearLabel="Clear service filter"
          onClear={() => onQueryChange('')}
        />
      )}
      {traceId && (
        <Chip
          name="trace"
          value={`${traceId.slice(0, 12)}…`}
          clearLabel="Clear trace filter"
          onClear={() => onTraceIdChange('')}
        />
      )}

      <StripSpacer />

      <Input
        type="search"
        aria-label="Filter by service"
        placeholder="service…"
        value={query}
        onChange={(e) => onQueryChange(e.target.value)}
        className="h-[22px] w-40 rounded-sm px-2 font-mono text-[10.5px]"
      />
      <Input
        type="search"
        aria-label="Filter by trace id"
        placeholder="trace id…"
        value={traceId}
        onChange={(e) => onTraceIdChange(e.target.value)}
        className="h-[22px] w-52 rounded-sm px-2 font-mono text-[10.5px]"
      />
    </Strip>
  );
}
