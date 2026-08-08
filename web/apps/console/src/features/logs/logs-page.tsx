import { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { getRouteApi } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { api } from '@/shared/api';
import { PageTemplate } from '@/shared/ui/app-shell';
import {
  Description,
  Error as ErrorGlyph,
  Hub,
  Pause,
  PlayArrow,
  Warning,
} from '@nine-thirty-five/material-symbols-react/rounded/400';
import { LogEntry } from '@/shared/ui/apm';
import {
  Actions,
  Blank,
  BlankText,
  Chip,
  LoadingRows,
  Mosaic,
  MultiFilter,
  Seg,
  StatBar,
  Strip,
  StripSpacer,
} from '@/shared/ui/console';
import { Button } from '@/shared/ui/primitives/button';
import { bucketize } from '@/shared/lib/buckets';
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

/**
 * Levels, coarsest first. Ordered by how often an operator reaches for them
 * rather than by severity: nobody opens a log viewer to read `trace` first.
 */
const LEVELS = ['error', 'warn', 'info', 'debug', 'trace', 'fatal'] as const;

type Level = (typeof LEVELS)[number];

/** How often live tail re-reads the window. Fast enough to feel live, slow
 *  enough that a busy stream does not re-render forty times a second. */
const TAIL_INTERVAL_MS = 5_000;

export function LogsPage() {
  const { t } = useTranslation();
  useDocumentTitle('Logs');

  // Same rule as the trace explorer: what you are looking at is in the URL, so
  // "logs for this trace id" is a link rather than a set of typed-in filters.
  const { stream, traceId, range: rangeParam } = routeApi.useSearch();
  const range = rangeParam ?? DEFAULT_TIME_RANGE;
  const navigate = routeApi.useNavigate();

  // Level filtering and tailing are workstation state, not a shared view: they
  // describe how you are watching, not what you are looking at.
  const [levels, setLevels] = useState<Level[]>([]);
  const [tailing, setTailing] = useState(false);

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
    refetchInterval: tailing ? TAIL_INTERVAL_MS : 30_000,
  });

  // An empty selection means no filter rather than no results: someone who
  // unticks every level wants the stream back, not an empty screen.
  const entries = useMemo(() => {
    const items = data?.items ?? [];
    if (levels.length === 0) return items;
    return items.filter((entry) => levels.includes(entry.level as Level));
  }, [data?.items, levels]);

  return (
    <PageTemplate
      title={t('logs.title')}
      subtitle={`${entries.length.toLocaleString()} of ${(data?.items.length ?? 0).toLocaleString()} · last ${range}`}
      actions={
        <Actions>
          <Button
            variant={tailing ? 'default' : 'outline'}
            className="h-[26px] gap-1.5 px-2.5 text-[11.5px]"
            onClick={() => setTailing((on) => !on)}
            title={`Re-read the window every ${TAIL_INTERVAL_MS / 1000} seconds`}
          >
            {tailing ? <Pause className="size-3.5" /> : <PlayArrow className="size-3.5" />}
            {tailing ? 'Tailing' : 'Live tail'}
          </Button>
        </Actions>
      }
    >
      <Toolbar
        range={range}
        onRangeChange={(next) => setSearch({ range: next })}
        query={stream ?? ''}
        onQueryChange={(next) => setSearch({ stream: next === '' ? undefined : next })}
        traceId={traceId ?? ''}
        onTraceIdChange={(next) => setSearch({ traceId: next === '' ? undefined : next })}
        levels={levels}
        onLevelsChange={setLevels}
      />

      {isError && (
        <Blank title="Could not read the log stream.">
          <BlankText>{(error as Error).message}</BlankText>
        </Blank>
      )}

      {!isLoading && !isError && entries.length > 0 && (
        <LogStats entries={entries} range={range} />
      )}

      {!isLoading && !isError && entries.length > 0 && (
        <Mosaic
          buckets={bucketize(
            entries,
            { startUnixMs, endUnixMs, count: 32 },
            (entry) => entry.timestamp,
            (entry) => entry.level === 'error' || entry.level === 'fatal',
          )}
          unitLabel="log entries"
          onSelect={() =>
            void navigate({ search: (prev) => ({ ...prev, range: '15m' }), replace: true })
          }
        />
      )}

      {isLoading ? (
        <LoadingRows count={12} />
      ) : entries.length === 0 ? (
        <Blank title="No log entries in this window.">
          <BlankText>
            {levels.length > 0
              ? `Nothing at ${levels.join(' or ')} level here. Clear the level filter, or widen the range.`
              : 'Widen the range, or clear the service and trace filters. A trace id filter shows only what that request emitted, which is often nothing.'}
          </BlankText>
        </Blank>
      ) : (
        <div className="log-stream">
          {entries.map((entry, i) => (
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
  levels: Level[];
  onLevelsChange: (next: Level[]) => void;
}

function Toolbar({
  range,
  onRangeChange,
  query,
  onQueryChange,
  traceId,
  onTraceIdChange,
  levels,
  onLevelsChange,
}: ToolbarProps) {
  return (
    <Strip>
      <Seg label="Time range" value={range} options={TIME_RANGES} onChange={onRangeChange} />

      {/* Levels are not exclusive: "warn and error" is the normal thing to want
          during an incident, and a segmented control cannot say it. */}
      <MultiFilter
        label="Log level"
        options={LEVELS}
        selected={levels}
        onChange={onLevelsChange}
        tone={(level) => `level-${level}`}
      />

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
