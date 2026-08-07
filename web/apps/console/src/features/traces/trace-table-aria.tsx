import { useMemo, useState } from 'react';
import {
  Cell,
  Column,
  Row,
  Table,
  TableBody,
  TableHeader,
  type SortDescriptor,
} from 'react-aria-components';
import { Duration, TimeFormat } from '@/shared/ui/apm';
import { cn } from '@/shared/lib/utils';
import type { TraceSummary, TraceStatus } from '@/shared/api/types';

/**
 * The trace list rebuilt on React Aria Components — a spike, kept beside the
 * hand-rolled table so the two can be compared on the screen that actually
 * needs collection machinery. Switch with `?table=aria` on /traces.
 *
 * What comes from the library rather than from us:
 *   • column sorting, including the aria-sort announcements
 *   • roving-tabindex keyboard navigation over cells and rows
 *   • row activation by Enter/Space, with correct focus rings
 *   • typeahead: start typing a service name to jump to that row
 *   • `grid` semantics, so a screen reader reads position and header context
 *
 * The hand-rolled table has none of that. Its rows are `<tr>` wrapping a link,
 * so a keyboard user tabs through every cell link in order with no notion of
 * rows or columns, and there is no sorting at all.
 *
 * Spike scope: sort state is local. Everything else in this app keeps view
 * state in the URL, and if this direction is adopted the sort belongs there too
 * — it is left out here so the experiment stays deletable in one file.
 */

type SortColumn = 'root' | 'status' | 'duration' | 'spans' | 'started';

export interface TraceTableAriaProps {
  traces: TraceSummary[];
  /** Called when a row is activated by click, Enter or Space. */
  onOpenTrace: (traceId: string) => void;
}

export function TraceTableAria({ traces, onOpenTrace }: TraceTableAriaProps) {
  const [sort, setSort] = useState<SortDescriptor>({
    column: 'started',
    direction: 'descending',
  });

  const rows = useMemo(() => sortTraces(traces, sort), [traces, sort]);

  if (traces.length === 0) {
    return (
      <div className="rounded-md border border-border bg-card p-8 text-center text-sm text-muted-foreground">
        No traces found in the selected time range.
      </div>
    );
  }

  return (
    <div className="overflow-hidden rounded-md border border-border bg-card">
      <Table
        aria-label="Traces"
        selectionMode="none"
        sortDescriptor={sort}
        onSortChange={setSort}
        onRowAction={(key) => onOpenTrace(String(key))}
        className="w-full text-sm outline-none"
      >
        <TableHeader className="border-b border-border bg-surface-3 text-left text-xs font-medium uppercase tracking-wider text-muted-foreground">
          <SortableColumn id="root" isRowHeader>
            Service · Operation
          </SortableColumn>
          <SortableColumn id="status">Status</SortableColumn>
          <SortableColumn id="duration" align="right">
            Duration
          </SortableColumn>
          <SortableColumn id="spans" align="right">
            Spans
          </SortableColumn>
          <SortableColumn id="started" align="right">
            Started
          </SortableColumn>
        </TableHeader>

        <TableBody
          items={rows}
          renderEmptyState={() => (
            <div className="p-8 text-center text-sm text-muted-foreground">No traces.</div>
          )}
        >
          {(trace) => (
            <Row
              id={trace.traceId}
              className={cn(
                'cursor-pointer border-b border-border outline-none last:border-b-0',
                'hover:bg-surface-2',
                // The library sets these; we only decide what they look like.
                'data-[focus-visible]:bg-surface-2 data-[focus-visible]:outline-2',
                'data-[focus-visible]:-outline-offset-2 data-[focus-visible]:outline-primary',
              )}
            >
              <Cell className="px-4 py-2 outline-none">
                <div className="font-medium text-foreground">{trace.rootService}</div>
                <div className="text-xs text-muted-foreground">{trace.rootOperation}</div>
              </Cell>
              <Cell className="px-4 py-2 outline-none">
                <StatusPill status={trace.status} />
              </Cell>
              <Cell className="px-4 py-2 text-right outline-none">
                <Duration ms={trace.durationMs} />
              </Cell>
              <Cell className="px-4 py-2 text-right text-muted-foreground outline-none">
                {trace.spanCount}
              </Cell>
              <Cell className="px-4 py-2 text-right outline-none">
                <TimeFormat ms={trace.startTime} relative />
              </Cell>
            </Row>
          )}
        </TableBody>
      </Table>
    </div>
  );
}

function SortableColumn({
  id,
  children,
  isRowHeader,
  align = 'left',
}: {
  id: SortColumn;
  children: React.ReactNode;
  isRowHeader?: boolean;
  align?: 'left' | 'right';
}) {
  return (
    <Column
      id={id}
      isRowHeader={isRowHeader}
      allowsSorting
      className={cn(
        'cursor-pointer px-4 py-2 outline-none',
        'data-[focus-visible]:outline-2 data-[focus-visible]:-outline-offset-2 data-[focus-visible]:outline-primary',
        align === 'right' && 'text-right',
      )}
    >
      {({ sortDirection }) => (
        <span className="inline-flex items-center gap-1">
          {children}
          {/* Direction is rendered for sighted users; the library already puts
              aria-sort on the header for everyone else. */}
          <span aria-hidden="true" className="text-[10px] text-muted-2">
            {sortDirection === 'ascending' ? '▲' : sortDirection === 'descending' ? '▼' : ''}
          </span>
        </span>
      )}
    </Column>
  );
}

/** Status order puts failures first when sorting ascending — the useful end. */
const STATUS_WEIGHT: Record<TraceStatus, number> = { error: 0, unset: 1, ok: 2 };

export function sortTraces(traces: TraceSummary[], sort: SortDescriptor): TraceSummary[] {
  const column = sort.column as SortColumn;
  const factor = sort.direction === 'descending' ? -1 : 1;

  const compare = (a: TraceSummary, b: TraceSummary): number => {
    switch (column) {
      case 'root':
        return `${a.rootService} ${a.rootOperation}`.localeCompare(
          `${b.rootService} ${b.rootOperation}`,
        );
      case 'status':
        return STATUS_WEIGHT[a.status] - STATUS_WEIGHT[b.status];
      case 'duration':
        return a.durationMs - b.durationMs;
      case 'spans':
        return a.spanCount - b.spanCount;
      case 'started':
        return a.startTime - b.startTime;
      default:
        return 0;
    }
  };

  // Copy first: sorting the query's array in place would mutate the React Query
  // cache, which hands the same reference to every consumer.
  return [...traces].sort((a, b) => compare(a, b) * factor);
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
