import type { Story, StoryDefault } from '@ladle/react';
import { TimeFormat } from '@/shared/ui/apm/time-format';

export default {
  title: 'APM / Time format',
} satisfies StoryDefault;

/**
 * Time formatter — absolute (RFC 3339) or relative ("2 min ago"). Auto-
 * ticks relative mode every 30 seconds.
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

/** Right-aligned in a table column (e.g. log viewer timestamp). */
export const InTableColumn: Story = () => (
  <div className="p-6 max-w-2xl">
    <table className="w-full text-sm">
      <thead>
        <tr className="border-b text-left text-xs uppercase text-muted-foreground">
          <th className="py-2 font-medium w-32">Time (relative)</th>
          <th className="py-2 font-medium w-48">Time (absolute)</th>
          <th className="py-2 font-medium">Message</th>
        </tr>
      </thead>
      <tbody className="font-mono text-xs">
        <tr className="border-b">
          <td className="py-1.5 text-right"><TimeFormat ms={NOW - 5_000}    relative /></td>
          <td className="py-1.5"><TimeFormat ms={NOW - 5_000}    format="time" /></td>
          <td className="py-1.5">accepted checkout request</td>
        </tr>
        <tr className="border-b">
          <td className="py-1.5 text-right"><TimeFormat ms={NOW - 60_000}   relative /></td>
          <td className="py-1.5"><TimeFormat ms={NOW - 60_000}   format="time" /></td>
          <td className="py-1.5">connection pool 80% used</td>
        </tr>
        <tr className="border-b">
          <td className="py-1.5 text-right"><TimeFormat ms={NOW - 600_000}  relative /></td>
          <td className="py-1.5"><TimeFormat ms={NOW - 600_000}  format="time" /></td>
          <td className="py-1.5">slow query detected</td>
        </tr>
        <tr>
          <td className="py-1.5 text-right"><TimeFormat ms={NOW - 86_400_000} relative /></td>
          <td className="py-1.5"><TimeFormat ms={NOW - 86_400_000} format="date" /></td>
          <td className="py-1.5">last successful backup</td>
        </tr>
      </tbody>
    </table>
  </div>
);

/**
 * Time-range picker — relative past + absolute end. Shows current time
 * with both representations side by side.
 */
export const TimeRangePicker: Story = () => (
  <div className="p-6 max-w-md">
    <div className="rounded-md border border-border bg-card p-4">
      <div className="text-xs text-muted-foreground mb-2">Time range</div>
      <div className="flex items-baseline gap-3">
        <span className="font-mono text-sm">
          <TimeFormat ms={NOW - 3_600_000} relative />
        </span>
        <span className="text-muted-foreground">→</span>
        <span className="font-mono text-sm">now</span>
      </div>
      <div className="text-xs text-muted-foreground mt-1">
        <TimeFormat ms={NOW - 3_600_000} format="iso" />
      </div>
    </div>
  </div>
);