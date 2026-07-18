import { useEffect, useState } from 'react';
import { cn } from '@/shared/lib/utils';

export interface TimeFormatProps {
  /** Unix epoch milliseconds. Required. */
  ms: number;
  /** Show relative time (default: false). */
  relative?: boolean;
  /** Custom format for absolute mode. Default: ISO 8601 / RFC 3339. */
  format?: 'iso' | 'date' | 'time';
  /** Tick interval for relative mode updates. Default: 30 seconds. */
  tickMs?: number;
  className?: string;
}

/**
 * Time display with absolute and relative modes.
 *
 * Absolute (default): ISO 8601 (`2026-07-19T14:08:21.412Z`), or date / time only.
 * Relative: `2 min ago`, updated every `tickMs` (default 30s).
 *
 * Usage:
 *   <TimeFormat ms={trace.startTime} />
 *   <TimeFormat ms={entry.timestamp} relative />
 *   <TimeFormat ms={entry.timestamp} format="date" />
 */
export function TimeFormat({
  ms,
  relative = false,
  format = 'iso',
  tickMs = 30_000,
  className,
}: TimeFormatProps) {
  const [, force] = useState(0);

  // Re-render periodically so "2 min ago" updates.
  useEffect(() => {
    if (!relative) return;
    const id = setInterval(() => force((n) => n + 1), tickMs);
    return () => clearInterval(id);
  }, [relative, tickMs]);

  const date = new Date(ms);
  const text = relative ? formatRelative(date) : formatAbsolute(date, format);

  return (
    <span
      className={cn('time', relative ? 'time-relative' : 'time-absolute', className)}
      title={formatAbsolute(date, 'iso')}
    >
      {text}
    </span>
  );
}

export function formatAbsolute(date: Date, format: 'iso' | 'date' | 'time' = 'iso'): string {
  switch (format) {
    case 'iso':
      return date.toISOString();
    case 'date':
      return date.toISOString().slice(0, 10);
    case 'time':
      return date.toISOString().slice(11, 19);
  }
}

export function formatRelative(date: Date, now: Date = new Date()): string {
  const diffMs = now.getTime() - date.getTime();
  if (diffMs < 0) return 'in the future';
  if (diffMs < 1_000) return 'just now';
  if (diffMs < 60_000) return `${Math.floor(diffMs / 1_000)} sec ago`;
  if (diffMs < 3_600_000) return `${Math.floor(diffMs / 60_000)} min ago`;
  if (diffMs < 86_400_000) return `${Math.floor(diffMs / 3_600_000)} hr ago`;
  if (diffMs < 30 * 86_400_000) return `${Math.floor(diffMs / 86_400_000)} days ago`;
  return formatAbsolute(date, 'date');
}