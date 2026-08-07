/**
 * Parsers for typed route search params.
 *
 * Search params are user-editable text: they arrive from a pasted link, a
 * bookmark from three versions ago, or someone editing the address bar. Every
 * parser here is total — it maps anything unexpected onto a default rather than
 * throwing, because a malformed query string should degrade the view, not blank
 * the app.
 */

/** Relative time windows the UI offers. */
export const TIME_RANGES = ['1h', '6h', '24h'] as const;

export type TimeRangeKey = (typeof TIME_RANGES)[number];

export const DEFAULT_TIME_RANGE: TimeRangeKey = '1h';

const RANGE_MS: Record<TimeRangeKey, number> = {
  '1h': 60 * 60 * 1000,
  '6h': 6 * 60 * 60 * 1000,
  '24h': 24 * 60 * 60 * 1000,
};

export function parseTimeRange(value: unknown): TimeRangeKey {
  return TIME_RANGES.includes(value as TimeRangeKey) ? (value as TimeRangeKey) : DEFAULT_TIME_RANGE;
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
