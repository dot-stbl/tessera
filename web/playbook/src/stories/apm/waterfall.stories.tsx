import type { Story, StoryDefault } from '@ladle/react';
import { Waterfall, type WaterfallSpan } from '@/shared/ui/apm/waterfall';

export default {
  title: 'APM / Waterfall',
} satisfies StoryDefault;

/**
 * Trace waterfall — Jaeger-style timeline of parent/child spans.
 * Each span renders at a horizontal offset computed from
 * `startOffsetMs / totalDurationMs`; bar width scales by
 * `durationMs / totalDurationMs`. Depth is shown via indentation.
 */

const sampleSpans: WaterfallSpan[] = [
  { id: 's1', name: 'POST /checkout', service: 'checkout-api', startOffsetMs: 0,    durationMs: 1247, status: 'ok' },
  { id: 's2', name: 'SELECT orders',  service: 'postgres',     startOffsetMs: 12,   durationMs: 50,   depth: 1, status: 'ok' },
  { id: 's3', name: 'SELECT items',   service: 'postgres',     startOffsetMs: 64,   durationMs: 23,   depth: 2, status: 'ok' },
  { id: 's4', name: 'INSERT audit',   service: 'postgres',     startOffsetMs: 90,   durationMs: 12,   depth: 2, status: 'ok' },
  { id: 's5', name: 'Charge',         service: 'stripe',       startOffsetMs: 95,   durationMs: 1180, depth: 1, status: 'error' },
  { id: 's6', name: 'Refund (idempotent)', service: 'stripe',  startOffsetMs: 1180, durationMs: 12,   depth: 2, status: 'idle' },
];

export const SampleTrace: Story = () => (
  <div className="p-6">
    <Waterfall spans={sampleSpans} totalDurationMs={1300} />
  </div>
);

export const LongTrace: Story = () => {
  const long: WaterfallSpan[] = Array.from({ length: 24 }, (_, i) => ({
    id: `s${i}`,
    name: `request ${i + 1}`,
    service: i === 0 ? 'api-gateway' : i % 3 === 0 ? 'postgres' : i % 3 === 1 ? 'redis' : 'kafka',
    startOffsetMs: i * 100,
    durationMs: 50 + Math.random() * 200,
    depth: i % 4,
    status: i % 7 === 0 ? 'error' : 'ok',
  }));
  return (
    <div className="p-6">
      <Waterfall spans={long} totalDurationMs={2400} />
    </div>
  );
};

export const EmptyTrace: Story = () => (
  <div className="p-6">
    <Waterfall spans={[]} totalDurationMs={0} />
  </div>
);