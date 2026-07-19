import type { Story, StoryDefault } from '@ladle/react';
import { Duration } from '@/shared/ui/apm/duration';

export default {
  title: 'APM / Duration',
} satisfies StoryDefault;

/**
 * Auto-scaling, tabular-numbers duration formatter. `<value> <unit>` scaled:
 *   < 1µs → ns  ·  < 1ms → µs  ·  < 1s → ms  ·  < 1m → s  ·  else m / h.
 *
 * Threshold highlighting (defaults):
 *   is-slow      > 1s     — amber tint
 *   is-very-slow > 5s     — red tint + bold
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

/**
 * In a table column — right-aligned tabular nums. Standard APM list view.
 */
export const InTableColumn: Story = () => (
  <div className="p-6">
    <table className="w-full text-sm">
      <thead>
        <tr className="border-b text-left text-xs uppercase text-muted-foreground">
          <th className="py-2 font-medium">Trace</th>
          <th className="py-2 font-medium">Service</th>
          <th className="py-2 font-medium text-right">Duration</th>
          <th className="py-2 font-medium">Status</th>
        </tr>
      </thead>
      <tbody className="font-mono">
        {[
          { id: '7f3a', service: 'checkout-api', ms: 1247,  status: 'ok' },
          { id: '8c4d', service: 'cart-svc',     ms: 38,    status: 'ok' },
          { id: '9e5f', service: 'pricing-svc',  ms: 1240,  status: 'ok' },
          { id: 'a1b2', service: 'stripe',       ms: 12_400, status: 'err' },
          { id: 'b3c4', service: 'postgres',     ms: 220,   status: 'ok' },
          { id: 'c5d6', service: 'redis',        ms: 4,     status: 'ok' },
        ].map((row) => (
          <tr key={row.id} className="border-b">
            <td className="py-1.5 text-xs text-muted-foreground">{row.id}…</td>
            <td className="py-1.5">{row.service}</td>
            <td className="py-1.5 text-right">
              <Duration ms={row.ms} />
            </td>
            <td className="py-1.5">
              <span className={row.status === 'ok' ? 'text-ok-ink' : 'text-err-ink'}>
                {row.status === 'ok' ? 'OK' : 'ERROR'}
              </span>
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  </div>
);

/** Latency histogram summary — p50/p95/p99 mix. */
export const Percentiles: Story = () => (
  <div className="p-6 max-w-md">
    <div className="rounded-md border border-border bg-card p-4">
      <div className="text-xs text-muted-foreground mb-3 font-medium uppercase tracking-wide">
        Request latency (last 1h)
      </div>
      <div className="grid grid-cols-3 gap-4 font-mono">
        <div>
          <div className="text-xs text-muted-foreground">p50</div>
          <div className="text-lg"><Duration ms={12} /></div>
        </div>
        <div>
          <div className="text-xs text-muted-foreground">p95</div>
          <div className="text-lg"><Duration ms={245} /></div>
        </div>
        <div>
          <div className="text-xs text-muted-foreground">p99</div>
          <div className="text-lg"><Duration ms={1240} /></div>
        </div>
      </div>
    </div>
  </div>
);