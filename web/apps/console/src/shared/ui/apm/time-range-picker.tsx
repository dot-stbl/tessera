import { useState } from 'react';
import { Check, KeyboardArrowDown } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { cn } from '@/shared/lib/utils';
import { Button } from '@/shared/ui/primitives/button';
import { Popover, PopoverContent, PopoverTrigger } from '@/shared/ui/primitives/popover';

export interface TimeRange {
  startUnixMs: number;
  endUnixMs: number;
}

export interface TimeRangeValue {
  /** Preset id when a relative preset is selected; absent for custom ranges. */
  preset?: string;
  /** Display label, e.g. "Last 1 hour" or "Custom range". */
  label: string;
  range: TimeRange;
}

export interface TimeRangePreset {
  id: string;
  label: string;
  durationMs: number;
}

export const TIME_RANGE_PRESETS: TimeRangePreset[] = [
  { id: '15m', label: 'Last 15 minutes', durationMs: 15 * 60_000 },
  { id: '1h', label: 'Last 1 hour', durationMs: 60 * 60_000 },
  { id: '6h', label: 'Last 6 hours', durationMs: 6 * 3_600_000 },
  { id: '24h', label: 'Last 24 hours', durationMs: 24 * 3_600_000 },
  { id: '7d', label: 'Last 7 days', durationMs: 7 * 86_400_000 },
  { id: '30d', label: 'Last 30 days', durationMs: 30 * 86_400_000 },
];

/** Resolve a relative preset to a concrete range anchored at `now`. */
export function presetRangeValue(preset: TimeRangePreset, now: number = Date.now()): TimeRangeValue {
  return {
    preset: preset.id,
    label: preset.label,
    range: { startUnixMs: now - preset.durationMs, endUnixMs: now },
  };
}

/** Convenient default: Last 1 hour, anchored at now. */
export function defaultTimeRange(now: number = Date.now()): TimeRangeValue {
  return presetRangeValue(TIME_RANGE_PRESETS[1], now);
}

const INPUT_CLASS =
  'h-7 rounded-md border border-input bg-input/20 px-2 font-mono text-xs text-foreground outline-none focus-visible:border-ring focus-visible:ring-2 focus-visible:ring-ring/30 dark:bg-input/30';

function toLocalInputValue(ms: number): string {
  const d = new Date(ms);
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

export interface TimeRangePickerProps {
  value: TimeRangeValue;
  onChange: (value: TimeRangeValue) => void;
  className?: string;
}

/**
 * Global time-range control (Grafana / Kibana style): relative presets +
 * a custom absolute range. Controlled — the parent holds `value` and a preset
 * resolves to a concrete `range` (anchored at selection time) on change.
 *
 * Usage:
 *   const [range, setRange] = useState(defaultTimeRange());
 *   <TimeRangePicker value={range} onChange={setRange} />
 */
export function TimeRangePicker({ value, onChange, className }: TimeRangePickerProps) {
  const [open, setOpen] = useState(false);
  const [from, setFrom] = useState(() => toLocalInputValue(value.range.startUnixMs));
  const [to, setTo] = useState(() => toLocalInputValue(value.range.endUnixMs));

  const selectPreset = (preset: TimeRangePreset) => {
    onChange(presetRangeValue(preset));
    setOpen(false);
  };

  const applyCustom = () => {
    const startUnixMs = new Date(from).getTime();
    const endUnixMs = new Date(to).getTime();
    if (Number.isNaN(startUnixMs) || Number.isNaN(endUnixMs) || startUnixMs >= endUnixMs) return;
    onChange({ label: 'Custom range', range: { startUnixMs, endUnixMs } });
    setOpen(false);
  };

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger render={<Button variant="outline" size="sm" className={cn('gap-1.5', className)} />}>
        {value.label}
        <KeyboardArrowDown className="size-3.5 text-muted-foreground" />
      </PopoverTrigger>
      <PopoverContent align="start" className="w-64 gap-0 p-0">
        <div className="flex flex-col p-1">
          {TIME_RANGE_PRESETS.map((preset) => {
            const active = value.preset === preset.id;
            return (
              <button
                key={preset.id}
                type="button"
                onClick={() => selectPreset(preset)}
                className={cn(
                  'flex items-center justify-between rounded-md px-2 py-1.5 text-left text-xs hover:bg-accent hover:text-accent-foreground',
                  active && 'bg-accent/60 font-medium text-foreground',
                )}
              >
                {preset.label}
                {active && <Check className="size-3.5" />}
              </button>
            );
          })}
        </div>
        <div className="border-t border-border p-2.5">
          <p className="mb-1.5 text-[10px] font-semibold tracking-wide text-muted-foreground uppercase">
            Custom range
          </p>
          <div className="flex flex-col gap-2">
            <label className="flex flex-col gap-1 text-[10px] text-muted-foreground">
              From
              <input
                type="datetime-local"
                value={from}
                onChange={(e) => setFrom(e.target.value)}
                className={INPUT_CLASS}
              />
            </label>
            <label className="flex flex-col gap-1 text-[10px] text-muted-foreground">
              To
              <input
                type="datetime-local"
                value={to}
                onChange={(e) => setTo(e.target.value)}
                className={INPUT_CLASS}
              />
            </label>
            <Button size="sm" onClick={applyCustom} className="mt-0.5">
              Apply
            </Button>
          </div>
        </div>
      </PopoverContent>
    </Popover>
  );
}
