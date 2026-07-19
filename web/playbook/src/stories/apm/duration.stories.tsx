import type { Story, StoryDefault } from '@ladle/react';
import { Duration } from '@/shared/ui/apm/duration';

export default {
  title: 'APM / Duration',
} satisfies StoryDefault;

/**
 * Duration — auto-scaling, tabular-numbers duration formatter.
 * Format: `<value> <unit>` scaled by magnitude:
 *   < 1µs → ns  ·  < 1ms → µs  ·  < 1s → ms  ·  < 1m → s  ·  else m / h.
 *
 * Threshold highlighting (defaults):
 *   is-slow      > 1s    — amber tint
 *   is-very-slow > 5s    — red tint + bold
 */
export const AllScales: Story = () => (
  <div className="flex flex-col gap-2 p-6 font-mono">
    <Duration ms={0.000123} />
    <Duration ms={0.5} />
    <Duration ms={2.4} />
    <Duration ms={124} />
    <Duration ms={999} />
    <Duration ms={1247} />
    <Duration ms={5300} />
    <Duration ms={60_000} />
    <Duration ms={3_600_000} />
  </div>
);

export const ThresholdHighlighting: Story = () => (
  <div className="flex flex-col gap-2 p-6 font-mono">
    <Duration ms={200} />
    <Duration ms={980} />
    <Duration ms={1500} />
    <Duration ms={4999} />
    <Duration ms={5000} />
    <Duration ms={12_500} />
  </div>
);

export const CustomThresholds: Story = () => (
  <div className="flex flex-col gap-2 p-6 font-mono">
    <div className="text-xs text-muted-foreground">
      slow &gt; 100ms, very-slow &gt; 500ms
    </div>
    <Duration ms={50} />
    <Duration ms={200} />
    <Duration ms={750} />
  </div>
);

export const ZeroAndNegative: Story = () => (
  <div className="flex flex-col gap-2 p-6 font-mono">
    <Duration ms={0} />
    <Duration ms={-1} />
  </div>
);