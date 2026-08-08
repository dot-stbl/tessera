/**
 * Bucketing for the volume mosaic.
 *
 * Kept out of the component so the trace list and the log stream count the same
 * way — the two are read side by side during an incident, and a spike that
 * lands in bucket 14 on one screen and bucket 15 on the other is worse than no
 * chart at all.
 */

export interface Bucket {
  startUnixMs: number;
  endUnixMs: number;
  /** Everything in the bucket, failures included. */
  total: number;
  /** The failing subset — errors, or warn-and-worse for logs. */
  bad: number;
}

export interface BucketWindow {
  startUnixMs: number;
  endUnixMs: number;
  /** How many buckets to cut the window into. */
  count: number;
}

export function bucketize<T>(
  items: readonly T[],
  window: BucketWindow,
  timeOf: (item: T) => number,
  isBad: (item: T) => boolean,
): Bucket[] {
  const { startUnixMs, endUnixMs, count } = window;
  const span = Math.max(endUnixMs - startUnixMs, 1);
  const width = span / count;

  const buckets: Bucket[] = Array.from({ length: count }, (_, index) => ({
    startUnixMs: Math.round(startUnixMs + index * width),
    endUnixMs: Math.round(startUnixMs + (index + 1) * width),
    total: 0,
    bad: 0,
  }));

  for (const item of items) {
    const at = timeOf(item);
    if (at < startUnixMs || at > endUnixMs) continue;
    // Clamped rather than dropped: an item landing exactly on endUnixMs would
    // index one past the end, and silently losing the most recent event is the
    // worst possible off-by-one on a screen watched during an incident.
    const index = Math.min(count - 1, Math.floor((at - startUnixMs) / width));
    const bucket = buckets[index];
    if (!bucket) continue;
    bucket.total += 1;
    if (isBad(item)) bucket.bad += 1;
  }

  return buckets;
}
