import { useMemo, useState } from 'react';
import { cn } from '@/shared/lib/utils';
import {
  DataTable,
  DataTableToolbar,
  type ColumnDef,
  type FilterValues,
} from '@/shared/ui/data-table';
import { StatusPill, type StatusVariant } from '@/shared/ui/primitives/status-pill';
import type { TraceStatus, TraceSummary } from '@/shared/api/types';
import { Duration } from './duration';
import { TimeFormat } from './time-format';
import { TraceId } from './trace-id';
import { serviceColorIndex } from './span-row';

type SortKey = 'startTime' | 'durationMs' | 'spanCount';
type SortDir = 'asc' | 'desc';

const STATUS_VARIANT: Record<TraceStatus, StatusVariant> = { ok: 'ok', error: 'err', unset: 'idle' };
const STATUS_LABEL: Record<TraceStatus, string> = { ok: 'OK', error: 'ERROR', unset: 'UNSET' };

export interface TraceTableProps {
  traces: TraceSummary[];
  /** ms threshold above which a duration is tinted "slow" (amber). Default 1000. */
  slowThresholdMs?: number;
  onTraceClick?: (trace: TraceSummary) => void;
  className?: string;
}

/**
 * Trace explorer table (Kibana APM / Jaeger style). Composes the generic
 * DataTable + DataTableToolbar with trace-specific columns: status pill,
 * trace id, root service·operation, a duration bar (relative to the slowest
 * trace in view), span count, service colour dots, and relative start time.
 *
 * Filtering (service/operation text + status) and sorting (duration / spans /
 * start, click the header) are client-side over the given `traces` — a
 * server-backed screen can pre-filter and pass the page in.
 *
 * Usage:
 *   <TraceTable traces={data.items} onTraceClick={(t) => navigate(t.traceId)} />
 */
export function TraceTable({
  traces,
  slowThresholdMs = 1000,
  onTraceClick,
  className,
}: TraceTableProps) {
  const [filters, setFilters] = useState<FilterValues>({});
  const [sort, setSort] = useState<{ key: SortKey; dir: SortDir }>({ key: 'startTime', dir: 'desc' });

  const maxDuration = useMemo(
    () => traces.reduce((m, t) => Math.max(m, t.durationMs), 0),
    [traces],
  );

  const rows = useMemo(() => {
    const service = filters.service?.toLowerCase().trim();
    const status = filters.status;
    const filtered = traces.filter((t) => {
      if (status && t.status !== status) return false;
      if (service) {
        const hay = [t.rootService, t.rootOperation, ...t.services].join(' ').toLowerCase();
        if (!hay.includes(service)) return false;
      }
      return true;
    });
    const dir = sort.dir === 'asc' ? 1 : -1;
    return [...filtered].sort((a, b) => (a[sort.key] - b[sort.key]) * dir);
  }, [traces, filters, sort]);

  const toggleSort = (key: SortKey) =>
    setSort((s) => (s.key === key ? { key, dir: s.dir === 'asc' ? 'desc' : 'asc' } : { key, dir: 'desc' }));

  const sortHeader = (label: string, key: SortKey, align: 'left' | 'right' = 'left') => (
    <button
      type="button"
      onClick={() => toggleSort(key)}
      className={cn(
        'inline-flex items-center gap-1 hover:text-foreground',
        align === 'right' && 'flex-row-reverse',
      )}
    >
      {label}
      <span className={cn('text-[9px]', sort.key === key ? 'text-foreground' : 'text-muted-foreground/40')}>
        {sort.key === key ? (sort.dir === 'asc' ? '▲' : '▼') : '▲'}
      </span>
    </button>
  );

  const columns: ColumnDef<TraceSummary>[] = [
    {
      id: 'status',
      header: 'Status',
      meta: {
        size: 'w-[96px]',
        filter: {
          type: 'select',
          param: 'status',
          placeholder: 'Any status',
          options: [
            { value: 'ok', label: 'OK' },
            { value: 'error', label: 'Error' },
            { value: 'unset', label: 'Unset' },
          ],
        },
      },
      cell: ({ row }) => (
        <StatusPill variant={STATUS_VARIANT[row.original.status]} size="sm">
          {STATUS_LABEL[row.original.status]}
        </StatusPill>
      ),
    },
    {
      id: 'traceId',
      header: 'Trace',
      meta: { size: 'w-[128px]' },
      cell: ({ row }) => <TraceId id={row.original.traceId} />,
    },
    {
      id: 'root',
      header: 'Root service · operation',
      meta: { filter: { type: 'text', param: 'service', placeholder: 'Filter service or operation…' } },
      cell: ({ row }) => (
        <span className="block max-w-[320px] truncate font-mono text-[11px]">
          <span className="text-muted-foreground">{row.original.rootService} · </span>
          <span className="text-foreground">{row.original.rootOperation}</span>
        </span>
      ),
    },
    {
      id: 'durationMs',
      header: () => sortHeader('Duration', 'durationMs', 'right'),
      meta: { size: 'w-[176px]', align: 'right' },
      cell: ({ row }) => {
        const slow = row.original.durationMs >= slowThresholdMs;
        const pct = maxDuration > 0 ? Math.max((row.original.durationMs / maxDuration) * 100, 2) : 0;
        return (
          <div className="flex items-center justify-end gap-2">
            <span className="relative h-1.5 w-16 overflow-hidden rounded-full bg-surface-2">
              <span
                className="absolute inset-y-0 left-0 rounded-full"
                style={{ width: `${pct}%`, background: slow ? 'var(--slow)' : 'var(--muted-2)' }}
              />
            </span>
            <Duration ms={row.original.durationMs} slowThresholdMs={slowThresholdMs} />
          </div>
        );
      },
    },
    {
      id: 'spanCount',
      header: () => sortHeader('Spans', 'spanCount', 'right'),
      meta: { size: 'w-[76px]', align: 'right' },
      cell: ({ row }) => (
        <span className="font-mono text-[11px] tabular-nums text-foreground">{row.original.spanCount}</span>
      ),
    },
    {
      id: 'services',
      header: 'Services',
      meta: { size: 'w-[120px]' },
      cell: ({ row }) => (
        <div className="flex items-center gap-1.5">
          <span className="flex gap-0.5">
            {row.original.services.slice(0, 4).map((s) => (
              <span
                key={s}
                title={s}
                className="size-2 rounded-[2px]"
                style={{ background: `var(--svc-${serviceColorIndex(s)})` }}
              />
            ))}
          </span>
          <span className="font-mono text-[11px] tabular-nums text-muted-foreground">
            {row.original.services.length}
          </span>
        </div>
      ),
    },
    {
      id: 'startTime',
      header: () => sortHeader('Start', 'startTime', 'right'),
      meta: { size: 'w-[104px]', align: 'right' },
      cell: ({ row }) => <TimeFormat ms={row.original.startTime} relative />,
    },
  ];

  return (
    <div className={cn('flex flex-col gap-3', className)}>
      <DataTableToolbar columns={columns} filters={filters} onFiltersChange={setFilters} />
      {rows.length > 0 ? (
        <DataTable
          columns={columns}
          data={rows}
          density="compact"
          getRowId={(t) => t.traceId}
          onRowClick={onTraceClick}
        />
      ) : (
        <div className="rounded-lg border border-border bg-card p-8 text-center text-sm text-muted-foreground">
          {traces.length === 0 ? 'No traces in this time range.' : 'No traces match the current filters.'}
        </div>
      )}
    </div>
  );
}
