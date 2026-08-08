import type { CSSProperties } from 'react';
import type { Bucket } from '@/shared/lib/buckets';
import { cn } from '@/shared/lib/utils';

/**
 * Volume over the window — the console's signature chart.
 *
 * Every APM draws this as smooth bars or a filled area. Tessera draws it as
 * **stacked tiles**, one tile per N events, because a tessera is a tile in a
 * mosaic and because the encoding is honestly better here: at operator volumes
 * a bucket holds three or four traces, and three-versus-four is countable in
 * tiles and a guess in a 2px bar. Failures stack at the bottom in the data red,
 * so the question "was it bad, and when" is answered by shape, not by hue.
 *
 * The chart's left edge is the listing's time gutter. Both are the same axis,
 * so a spike above lines up with the rows that caused it.
 *
 * A column is a button: click narrows the window to that bucket. That makes the
 * chart the primary time control rather than a picture of one — which is the
 * whole difference between a dashboard and an instrument.
 */

const MAX_TILES = 7;

export interface MosaicProps {
  buckets: Bucket[];
  /** Plural noun for the thing being counted: "traces", "log entries". */
  unitLabel: string;
  /** Narrow the window to one bucket. Omit to render a read-only chart. */
  onSelect?: (startUnixMs: number, endUnixMs: number) => void;
  className?: string;
}

export function Mosaic({ buckets, unitLabel, onSelect, className }: MosaicProps) {
  const peak = Math.max(...buckets.map((bucket) => bucket.total), 1);
  // One tile is one event until a bucket outgrows the column, then tiles carry
  // more. Stated in the caption rather than left for the reader to infer.
  const per = Math.max(1, Math.ceil(peak / MAX_TILES));
  const total = buckets.reduce((sum, bucket) => sum + bucket.total, 0);
  const bad = buckets.reduce((sum, bucket) => sum + bucket.bad, 0);

  return (
    <div className={cn('mosaic', className)}>
      <div
        className="mosaic-plot"
        role="img"
        aria-label={`${total} ${unitLabel} over the window, ${bad} failing, in ${buckets.length} time buckets`}
      >
        {buckets.map((bucket) => (
          <MosaicColumn
            key={bucket.startUnixMs}
            bucket={bucket}
            per={per}
            unitLabel={unitLabel}
            onSelect={onSelect}
          />
        ))}
      </div>
      <p className="mosaic-caption">
        {per === 1 ? `1 tile = 1 ${unitLabel.replace(/s$/, '')}` : `1 tile = ${per} ${unitLabel}`}
        {onSelect && ' · click a column to narrow the window'}
      </p>
    </div>
  );
}

function MosaicColumn({
  bucket,
  per,
  unitLabel,
  onSelect,
}: {
  bucket: Bucket;
  per: number;
  unitLabel: string;
  onSelect?: (startUnixMs: number, endUnixMs: number) => void;
}) {
  const tiles = Math.ceil(bucket.total / per);
  const badTiles = Math.min(tiles, Math.ceil(bucket.bad / per));
  const at = new Date(bucket.startUnixMs).toLocaleTimeString(undefined, {
    hour: '2-digit',
    minute: '2-digit',
  });
  const title = `${at} · ${bucket.total} ${unitLabel}${bucket.bad > 0 ? `, ${bucket.bad} failing` : ''}`;

  const stack = (
    <span className="mosaic-stack" aria-hidden="true">
      {Array.from({ length: tiles }, (_, index) => (
        <i key={index} className={index < badTiles ? 'is-bad' : undefined} />
      ))}
      {/* An empty bucket still gets a floor mark. Without it a quiet stretch
       * looks like missing data rather than like nothing happening. */}
      {tiles === 0 && <i className="is-empty" />}
    </span>
  );

  if (!onSelect) {
    return (
      <span className="mosaic-col" title={title}>
        {stack}
      </span>
    );
  }

  return (
    <button
      type="button"
      className="mosaic-col"
      title={title}
      aria-label={title}
      disabled={bucket.total === 0}
      onClick={() => onSelect(bucket.startUnixMs, bucket.endUnixMs)}
    >
      {stack}
    </button>
  );
}

/** Fixed-height placeholder so the chart does not pop the page down on load. */
export function MosaicSkeleton({ columns = 48 }: { columns?: number }) {
  return (
    <div className="mosaic" aria-hidden="true">
      <div className="mosaic-plot is-loading">
        {Array.from({ length: columns }, (_, index) => (
          <span key={index} className="mosaic-col" style={{ '--i': index } as CSSProperties}>
            <span className="mosaic-stack">
              <i className="is-empty" />
            </span>
          </span>
        ))}
      </div>
      <p className="mosaic-caption">counting…</p>
    </div>
  );
}
