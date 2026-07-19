import { useState } from 'react';
import { type DateRange } from 'react-day-picker';
import { Check, KeyboardArrowDown } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { cn } from '@/shared/lib/utils';
import { Button } from '@/shared/ui/primitives/button';
import { Calendar } from '@/shared/ui/primitives/calendar';
import { Input } from '@/shared/ui/primitives/input';
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

const HHMM = /^(\d{1,2}):(\d{2})$/;

function toHhmm(ms: number): string {
  const d = new Date(ms);
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

function parseHhmm(value: string): { h: number; m: number } | null {
  const match = HHMM.exec(value.trim());
  if (!match) return null;
  const h = Number(match[1]);
  const m = Number(match[2]);
  if (h > 23 || m > 59) return null;
  return { h, m };
}

/** Combine a calendar day with an HH:MM time into a unix-ms timestamp. */
function combine(day: Date, time: { h: number; m: number }): number {
  const d = new Date(day);
  d.setHours(time.h, time.m, 0, 0);
  return d.getTime();
}

export interface TimeRangePickerProps {
  value: TimeRangeValue;
  onChange: (value: TimeRangeValue) => void;
  className?: string;
}

/**
 * Global time-range control (Grafana / Kibana style): relative presets + a
 * custom absolute range built from an on-brand range Calendar plus HH:MM time
 * fields (react-day-picker is date-only, so time lives in its own inputs).
 * Controlled — the parent holds `value`; a preset resolves to a concrete
 * `range` anchored at selection time.
 *
 * Usage:
 *   const [range, setRange] = useState(defaultTimeRange());
 *   <TimeRangePicker value={range} onChange={setRange} />
 */
export function TimeRangePicker({ value, onChange, className }: TimeRangePickerProps) {
  const [open, setOpen] = useState(false);
  const [days, setDays] = useState<DateRange | undefined>(() => ({
    from: new Date(value.range.startUnixMs),
    to: new Date(value.range.endUnixMs),
  }));
  const [fromTime, setFromTime] = useState(() => toHhmm(value.range.startUnixMs));
  const [toTime, setToTime] = useState(() => toHhmm(value.range.endUnixMs));

  const parsedFrom = parseHhmm(fromTime);
  const parsedTo = parseHhmm(toTime);
  const canApply = !!days?.from && !!days?.to && !!parsedFrom && !!parsedTo;

  const selectPreset = (preset: TimeRangePreset) => {
    onChange(presetRangeValue(preset));
    setOpen(false);
  };

  const applyCustom = () => {
    if (!days?.from || !days?.to || !parsedFrom || !parsedTo) return;
    const startUnixMs = combine(days.from, parsedFrom);
    const endUnixMs = combine(days.to, parsedTo);
    if (startUnixMs >= endUnixMs) return;
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
          <p className="mb-1 text-[10px] font-semibold tracking-wide text-muted-foreground uppercase">
            Custom range
          </p>
          <Calendar
            mode="range"
            selected={days}
            onSelect={(next) => setDays(next)}
            className="p-0"
          />
          <div className="mt-2 grid grid-cols-2 gap-2">
            <label className="flex flex-col gap-1 text-[10px] text-muted-foreground">
              From
              <Input
                value={fromTime}
                onChange={(e) => setFromTime(e.target.value)}
                placeholder="HH:MM"
                inputMode="numeric"
                aria-invalid={!parsedFrom || undefined}
                className="font-mono"
              />
            </label>
            <label className="flex flex-col gap-1 text-[10px] text-muted-foreground">
              To
              <Input
                value={toTime}
                onChange={(e) => setToTime(e.target.value)}
                placeholder="HH:MM"
                inputMode="numeric"
                aria-invalid={!parsedTo || undefined}
                className="font-mono"
              />
            </label>
          </div>
          <Button size="sm" onClick={applyCustom} disabled={!canApply} className="mt-2 w-full">
            Apply
          </Button>
        </div>
      </PopoverContent>
    </Popover>
  );
}
