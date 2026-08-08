import type { CSSProperties, KeyboardEvent, ReactNode } from 'react';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/shared/ui/primitives/table';
import { cn } from '@/shared/lib/utils';

/**
 * The listing — the console's one tabular surface.
 *
 * Built on the vendored shadcn `Table` rather than on raw table markup: that
 * keeps the structure, the container's horizontal scroll and the `data-slot`
 * hooks, and this layer only adds what shadcn has no opinion about — Tessera's
 * density, the time gutter, and the failure colouring. The visual side is the
 * unlayered `.listing` block in index.css, which outranks shadcn's utilities
 * without either side needing `!important`.
 *
 * Traces, spans and services are all "many rows, scan for the bad one", so they
 * are one component rather than three tables that drift apart.
 */

export function Listing({ children, className }: { children: ReactNode; className?: string }) {
  return <Table className={cn('listing', className)}>{children}</Table>;
}

export function ListingHead({ children }: { children: ReactNode }) {
  return (
    <TableHeader>
      <TableRow>{children}</TableRow>
    </TableHeader>
  );
}

export function ListingBody({ children }: { children: ReactNode }) {
  return <TableBody>{children}</TableBody>;
}

export interface ColumnProps {
  children?: ReactNode;
  /** Fixed width in px. Omit on the column that should absorb the slack. */
  width?: number;
  align?: 'left' | 'right';
  /** Marks the time gutter — the fixed first column carrying "when". */
  when?: boolean;
}

export function Column({ children, width, align, when }: ColumnProps) {
  return (
    <TableHead
      className={cn(when && 'col-when')}
      style={{ ...(width ? { width } : {}), ...(align === 'right' ? { textAlign: 'right' } : {}) }}
    >
      {children}
    </TableHead>
  );
}

export interface RowProps {
  children: ReactNode;
  /** Drives the failure colouring on the gutter, the track and the row. */
  status?: 'ok' | 'error' | 'unset';
  slow?: boolean;
  current?: boolean;
  /** Makes the whole row the link. Adds keyboard activation and a focus ring. */
  onOpen?: () => void;
}

export function Row({ children, status, slow, current, onOpen }: RowProps) {
  const handleKeyDown = (event: KeyboardEvent<HTMLTableRowElement>) => {
    if (!onOpen) return;
    // Space as well as Enter: the row advertises itself as a link but behaves
    // like a button, and an operator hitting space on a focused row expects it
    // to open rather than to scroll the page out from under them.
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      onOpen();
    }
  };

  return (
    <TableRow
      data-status={status}
      data-slow={slow ? 'true' : undefined}
      data-current={current ? 'true' : undefined}
      onClick={onOpen}
      onKeyDown={handleKeyDown}
      tabIndex={onOpen ? 0 : undefined}
      role={onOpen ? 'link' : undefined}
      className={cn(
        onOpen &&
          'cursor-pointer outline-none focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-primary',
      )}
    >
      {children}
    </TableRow>
  );
}

export function Cell({ children, className }: { children?: ReactNode; className?: string }) {
  return <TableCell className={className}>{children}</TableCell>;
}

/** The time gutter cell. Always first, always `--gutter` wide, on every screen. */
export function WhenCell({ children }: { children: ReactNode }) {
  return <TableCell className="col-when">{children}</TableCell>;
}

/** A machine value, right-aligned and tabular. `bad` states it in the data red. */
export function NumCell({ children, bad }: { children: ReactNode; bad?: boolean }) {
  return <TableCell className={cn('cell-num', bad && 'is-bad')}>{children}</TableCell>;
}

/**
 * A proportional bar. `share` is 0–100 and is the caller's job to scale, because
 * the right scale differs per screen: latency is log-normal, an error rate is
 * ranked against the worst row on screen.
 */
export function TrackCell({ share }: { share: number }) {
  return (
    <TableCell className="cell-track" style={{ '--share': share } as CSSProperties}>
      {share > 0 && <i />}
    </TableCell>
  );
}
