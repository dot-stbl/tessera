import { cn } from '@/shared/lib/utils';

export interface DurationProps {
  /** Duration in milliseconds. Required. */
  ms: number;
  /** Threshold above which the value gets `is-slow` styling. Default 1000. */
  slowThresholdMs?: number;
  /** Threshold above which the value gets `is-very-slow` styling. Default 5000. */
  verySlowThresholdMs?: number;
  className?: string;
}

/**
 * Auto-scaling compact duration display.
 *
 * Format rules:
 *   - < 1µs   → nanoseconds (`ns`)
 *   - < 1ms   → microseconds (`µs`)
 *   - < 1s    → milliseconds (`ms`)
 *   - < 1m    → seconds (`s`)
 *   - < 1h    → minutes (`m`)
 *   - otherwise → hours (`h`)
 *
 * Threshold highlighting:
 *   - `is-slow`        — > slowThresholdMs (default 1s, yellow tint)
 *   - `is-very-slow`   — > verySlowThresholdMs (default 5s, red tint)
 *
 * Usage:
 *   <Duration ms={1247} />
 *   <Duration ms={245} slowThresholdMs={500} />
 */
export function Duration({
  ms,
  slowThresholdMs = 1000,
  verySlowThresholdMs = 5000,
  className,
}: DurationProps) {
  const formatted = formatDuration(ms);
  const isVerySlow = ms > verySlowThresholdMs;
  const isSlow = !isVerySlow && ms > slowThresholdMs;

  return (
    <span
      className={cn(
        'duration',
        'mono-num',
        isVerySlow && 'duration-very-slow',
        isSlow && 'duration-slow',
        className,
      )}
    >
      {formatted.value}
      <span className="duration-unit">{formatted.unit}</span>
    </span>
  );
}

/**
 * Format a duration to { value, unit } without wrapping in JSX.
 */
export function formatDuration(ms: number): { value: string; unit: string } {
  if (ms < 0) return { value: '0', unit: 'ms' };
  if (ms < 0.001) return { value: (ms * 1_000_000).toFixed(0), unit: 'ns' };
  if (ms < 1) return { value: (ms * 1_000).toFixed(1), unit: 'µs' };
  if (ms < 1_000) return { value: ms.toFixed(0), unit: 'ms' };
  if (ms < 60_000) return { value: (ms / 1_000).toFixed(1), unit: 's' };
  if (ms < 3_600_000) return { value: (ms / 60_000).toFixed(1), unit: 'm' };
  return { value: (ms / 3_600_000).toFixed(1), unit: 'h' };
}