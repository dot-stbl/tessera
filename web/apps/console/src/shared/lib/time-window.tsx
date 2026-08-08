import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from 'react';
import type { Bucket } from '@/shared/lib/buckets';

/**
 * The global time window, and the volume the spine draws for it.
 *
 * Time is app state, not page state. Following a failure from a trace to its
 * logs is the thing this product exists to do, and it only works if the window
 * survives the navigation — previously each screen owned its own range and
 * every hop silently reset you to the last hour.
 *
 * Screens do not own the range. They read it, and they *publish* the volume of
 * whatever they are showing so the chrome can draw it. That inversion is what
 * lets one band at the top of the app describe six different screens.
 */

export const RANGES = ['15m', '1h', '6h', '24h', '7d'] as const;

export type Range = (typeof RANGES)[number];

export const RANGE_MS: Record<Range, number> = {
  '15m': 15 * 60 * 1000,
  '1h': 60 * 60 * 1000,
  '6h': 6 * 60 * 60 * 1000,
  '24h': 24 * 60 * 60 * 1000,
  '7d': 7 * 24 * 60 * 60 * 1000,
};

export const DEFAULT_RANGE: Range = '1h';

export function parseRange(value: unknown): Range {
  return RANGES.includes(value as Range) ? (value as Range) : DEFAULT_RANGE;
}

/** What a screen hands the spine to draw. */
export interface SpineSource {
  buckets: Bucket[];
  /** Plural noun for the counted thing: "traces", "log entries". */
  unitLabel: string;
}

interface TimeWindowValue {
  range: Range;
  setRange: (next: Range) => void;
  startUnixMs: number;
  endUnixMs: number;
  source: SpineSource | null;
  publish: (source: SpineSource | null) => void;
}

const TimeWindowContext = createContext<TimeWindowValue | null>(null);

const STORAGE_KEY = 'tessera-range';

/**
 * `endUnixMs` is quantized so it is stable between renders. It ends up inside
 * React Query keys, and a raw `Date.now()` there makes every render a cache
 * miss — the skeleton never clears and requests fire in a loop. Quantizing
 * keeps the key stable *and* lets the window advance on its own, which a value
 * frozen in state would not.
 */
export function resolveWindow(
  range: Range,
  bucketMs = 15_000,
  now: number = Date.now(),
): { startUnixMs: number; endUnixMs: number } {
  const endUnixMs = Math.floor(now / bucketMs) * bucketMs;
  return { startUnixMs: endUnixMs - RANGE_MS[range], endUnixMs };
}

export function TimeWindowProvider({ children }: { children: ReactNode }) {
  const [range, setRangeState] = useState<Range>(() => {
    if (typeof window === 'undefined') return DEFAULT_RANGE;
    return parseRange(window.localStorage.getItem(STORAGE_KEY));
  });
  const [source, setSource] = useState<SpineSource | null>(null);

  // The window is held in state rather than derived, because it has to advance
  // on its own: an operator leaves this tab open for an hour and expects "last
  // 1h" to still mean the last hour. Re-resolved on every range change and once
  // a minute after that.
  const [bounds, setBounds] = useState(() => resolveWindow(range));
  useEffect(() => {
    setBounds(resolveWindow(range));
    const id = window.setInterval(() => setBounds(resolveWindow(range)), 60_000);
    return () => window.clearInterval(id);
  }, [range]);

  const setRange = useCallback((next: Range) => {
    setRangeState(next);
    window.localStorage.setItem(STORAGE_KEY, next);
  }, []);

  const value = useMemo<TimeWindowValue>(
    () => ({
      range,
      setRange,
      startUnixMs: bounds.startUnixMs,
      endUnixMs: bounds.endUnixMs,
      source,
      publish: setSource,
    }),
    [range, setRange, bounds, source],
  );

  return <TimeWindowContext.Provider value={value}>{children}</TimeWindowContext.Provider>;
}

export function useTimeWindow(): TimeWindowValue {
  const ctx = useContext(TimeWindowContext);
  if (ctx === null) throw new Error('useTimeWindow must be used within a TimeWindowProvider');
  return ctx;
}

/**
 * Hand the spine this screen's volume. Cleared on unmount so a screen that has
 * nothing to say leaves the band empty rather than showing the previous
 * screen's data under a new title.
 */
export function usePublishSpine(buckets: Bucket[] | null, unitLabel: string): void {
  const { publish } = useTimeWindow();

  useEffect(() => {
    publish(buckets === null ? null : { buckets, unitLabel });
    return () => publish(null);
  }, [publish, buckets, unitLabel]);
}
