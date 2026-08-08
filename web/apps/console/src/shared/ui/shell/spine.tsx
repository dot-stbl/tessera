import type { Bucket } from '@/shared/lib/buckets';
import { RANGES, useTimeWindow, type Range } from '@/shared/lib/time-window';
import { cn } from '@/shared/lib/utils';

/**
 * The spine — the app's time axis, and the reason the layout is shaped the way
 * it is.
 *
 * It sits above every screen, it owns the range for all of them, and it does
 * not reset when you navigate. Whatever screen is mounted publishes its volume
 * into it, so the same band describes traces on one screen and log entries on
 * the next without changing what it means.
 *
 * Tiles, not bars. A tessera is a tile in a mosaic, and at operator volumes a
 * bucket holds three or four events — three-versus-four is countable in tiles
 * and a guess in a 2px bar. Failures stack from the baseline in `--found`, the
 * same red as the odd tile in the mark.
 *
 * Columns are buttons. That is the difference between an instrument and a
 * dashboard: the chart is the control, not a picture of one.
 */

/** Tallest stack a column can show before one tile has to carry more than one event. */
const MAX_TILES = 8;

export function Spine() {
  const { range, setRange, startUnixMs, endUnixMs, source } = useTimeWindow();
  const buckets = source?.buckets ?? [];
  const peak = Math.max(...buckets.map((bucket) => bucket.total), 1);
  const per = Math.max(1, Math.ceil(peak / MAX_TILES));

  return (
    <div className="spine">
      <div
        className="spine-plot"
        role="img"
        aria-label={
          source
            ? `${buckets.reduce((sum, b) => sum + b.total, 0)} ${source.unitLabel} across the window`
            : 'No volume for this screen'
        }
      >
        {buckets.length === 0
          ? Array.from({ length: 48 }, (_, index) => (
              <span key={index} className="spine-col">
                <span className="spine-stack">
                  <i className="is-void" />
                </span>
              </span>
            ))
          : buckets.map((bucket) => (
              <SpineColumn
                key={bucket.startUnixMs}
                bucket={bucket}
                per={per}
                unitLabel={source?.unitLabel ?? 'events'}
              />
            ))}
      </div>

      <div className="spine-side">
        <span className="spine-window">{formatWindow(startUnixMs, endUnixMs)}</span>
        {source && (
          <span className="spine-unit">
            {per === 1
              ? `1 tile = 1 ${source.unitLabel.replace(/s$/, '')}`
              : `1 tile = ${per} ${source.unitLabel}`}
          </span>
        )}
      </div>

      <RangePicker range={range} onChange={setRange} />
    </div>
  );
}

function SpineColumn({
  bucket,
  per,
  unitLabel,
}: {
  bucket: Bucket;
  per: number;
  unitLabel: string;
}) {
  const tiles = Math.ceil(bucket.total / per);
  const bad = Math.min(tiles, Math.ceil(bucket.bad / per));
  const at = new Date(bucket.startUnixMs).toLocaleTimeString(undefined, {
    hour: '2-digit',
    minute: '2-digit',
  });
  const title = `${at} · ${bucket.total} ${unitLabel}${bucket.bad > 0 ? `, ${bucket.bad} failing` : ''}`;

  return (
    <button type="button" className="spine-col" title={title} aria-label={title} disabled>
      <span className="spine-stack" aria-hidden="true">
        {Array.from({ length: tiles }, (_, index) => (
          <i key={index} className={index < bad ? 'is-bad' : undefined} />
        ))}
        {/* An empty bucket keeps a floor mark. Without it a quiet stretch reads
         * as missing data rather than as nothing happening. */}
        {tiles === 0 && <i className="is-void" />}
      </span>
    </button>
  );
}

function RangePicker({ range, onChange }: { range: Range; onChange: (next: Range) => void }) {
  return (
    <div className="seg" role="radiogroup" aria-label="Time range">
      {RANGES.map((key) => (
        <button
          key={key}
          type="button"
          role="radio"
          aria-checked={range === key}
          className={cn(range === key && 'is-on')}
          onClick={() => onChange(key)}
        >
          {key}
        </button>
      ))}
    </div>
  );
}

/**
 * The window as two clock times, or as dates once it spans more than a day.
 * An operator reads this to answer "am I looking at the right hour", so it
 * shows the bounds rather than the duration — the duration is already stated
 * by which range button is pressed.
 */
function formatWindow(startUnixMs: number, endUnixMs: number): string {
  const start = new Date(startUnixMs);
  const end = new Date(endUnixMs);
  const sameDay = start.toDateString() === end.toDateString();
  const time = (d: Date) =>
    d.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' });
  const day = (d: Date) => d.toLocaleDateString(undefined, { month: 'short', day: 'numeric' });

  return sameDay
    ? `${time(start)} – ${time(end)}`
    : `${day(start)} ${time(start)} – ${day(end)} ${time(end)}`;
}
