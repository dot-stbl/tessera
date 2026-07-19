import { useState } from 'react';
import type { Story, StoryDefault } from '@ladle/react';
import {
  TimeRangePicker,
  defaultTimeRange,
  type TimeRangeValue,
} from '@/shared/ui/apm/time-range-picker';

export default {
  title: 'APM / TimeRangePicker',
} satisfies StoryDefault;

/**
 * Global time-range control — relative presets + a custom absolute range.
 * Controlled: the parent holds the value; presets resolve to a concrete
 * start/end anchored at selection time.
 */
export const Default: Story = () => {
  const [value, setValue] = useState<TimeRangeValue>(() => defaultTimeRange());
  return (
    <div className="flex flex-col gap-3 p-6">
      <TimeRangePicker value={value} onChange={setValue} />
      <pre className="max-w-md rounded-md border border-border bg-card p-3 font-mono text-[11px] text-muted-foreground">
        {JSON.stringify(value, null, 2)}
      </pre>
    </div>
  );
};
