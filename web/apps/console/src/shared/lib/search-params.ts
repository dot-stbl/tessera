/**
 * Parsers for typed route search params.
 *
 * Search params are user-editable text: they arrive from a pasted link, a
 * bookmark from three versions ago, or someone editing the address bar. Every
 * parser here is total — it maps anything unexpected onto a default rather than
 * throwing, because a malformed query string should degrade the view, not blank
 * the app.
 */

/**
 * Relative time windows the UI offers. `15m` and `7d` were added because the two
 * real questions are "what is happening right now" and "did this start today" —
 * an hour is too coarse for the first and too short for the second.
 */
export const TIME_RANGES = ['15m', '1h', '6h', '24h', '7d'] as const;

export type TimeRangeKey = (typeof TIME_RANGES)[number];

export const DEFAULT_TIME_RANGE: TimeRangeKey = '1h';

const RANGE_MS: Record<TimeRangeKey, number> = {
  '15m': 15 * 60 * 1000,
  '1h': 60 * 60 * 1000,
  '6h': 6 * 60 * 60 * 1000,
  '24h': 24 * 60 * 60 * 1000,
  '7d': 7 * 24 * 60 * 60 * 1000,
};

export function parseTimeRange(value: unknown): TimeRangeKey {
  return TIME_RANGES.includes(value as TimeRangeKey) ? (value as TimeRangeKey) : DEFAULT_TIME_RANGE;
}

/**
 * Which slice of the list to show. `slow` is a client-side cut at 1s rather than
 * a backend `minDurationMs`: the threshold that matters is relative to the
 * window you are looking at, and 1s is the point past which a request stops
 * being a page load and starts being a complaint.
 */
export const TRACE_STATUS_FILTERS = ['all', 'errors', 'slow'] as const;

export type TraceStatusFilter = (typeof TRACE_STATUS_FILTERS)[number];

export const SLOW_THRESHOLD_MS = 1000;

export function parseTraceStatusFilter(value: unknown): TraceStatusFilter {
  return TRACE_STATUS_FILTERS.includes(value as TraceStatusFilter)
    ? (value as TraceStatusFilter)
    : 'all';
}

/**
 * How the trace list is ordered. Named pairs rather than a `field:direction`
 * tuple: it keeps the URL readable — `?sort=slowest` says what you are looking
 * at, `?sortBy=duration&order=desc` makes you assemble it.
 */
export const TRACE_SORTS = [
  'recent',
  'oldest',
  'slowest',
  'fastest',
  'widest',
  'narrowest',
] as const;

export type TraceSort = (typeof TRACE_SORTS)[number];

export function parseTraceSort(value: unknown): TraceSort {
  return TRACE_SORTS.includes(value as TraceSort) ? (value as TraceSort) : 'recent';
}

/** A non-empty trimmed string, or undefined. Empty means "no filter". */
export function parseOptionalString(value: unknown): string | undefined {
  if (typeof value !== 'string') return undefined;
  const trimmed = value.trim();
  return trimmed === '' ? undefined : trimmed;
}

/**
 * Resolve a relative range into an absolute window.
 *
 * `endUnixMs` is quantized to `bucketMs` so it is stable across renders — it
 * ends up inside a React Query key, and a raw `Date.now()` there makes every
 * render a cache miss, which pins the UI in its loading state and fires
 * requests in a loop. Quantizing keeps the key stable *and* lets the window
 * advance on its own, which a value frozen in state would not do.
 */
export function resolveTimeWindow(
  range: TimeRangeKey,
  bucketMs = 15_000,
  now: number = Date.now(),
): { startUnixMs: number; endUnixMs: number } {
  const endUnixMs = Math.floor(now / bucketMs) * bucketMs;
  return { startUnixMs: endUnixMs - RANGE_MS[range], endUnixMs };
}
