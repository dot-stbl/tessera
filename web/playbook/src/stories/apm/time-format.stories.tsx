import type { Story, StoryDefault } from '@ladle/react';
import { TimeFormat } from '@/shared/ui/apm/time-format';

export default {
  title: 'APM / Time format',
} satisfies StoryDefault;

/**
 * Time formatter — absolute (RFC 3339) or relative ("2 min ago").
 * Used by trace list (start time), log viewer (timestamp), and span
 * detail panels. Auto-ticks relative mode every 30s.
 */

const NOW = Date.now();

export const Absolute: Story = () => (
  <div className="flex flex-col gap-2 p-6 font-mono">
    <TimeFormat ms={NOW} />
    <TimeFormat ms={NOW - 60_000} />
    <TimeFormat ms={NOW - 3_600_000} />
    <TimeFormat ms={NOW - 86_400_000} />
  </div>
);

export const Relative: Story = () => (
  <div className="flex flex-col gap-2 p-6 font-mono">
    <TimeFormat ms={NOW - 5_000}     relative />
    <TimeFormat ms={NOW - 60_000}    relative />
    <TimeFormat ms={NOW - 600_000}   relative />
    <TimeFormat ms={NOW - 3_600_000} relative />
    <TimeFormat ms={NOW - 86_400_000} relative />
  </div>
);

export const CustomFormat: Story = () => (
  <div className="flex flex-col gap-2 p-6 font-mono">
    <TimeFormat ms={NOW} format="iso" />
    <TimeFormat ms={NOW} format="date" />
    <TimeFormat ms={NOW} format="time" />
  </div>
);