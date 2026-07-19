import { useState } from 'react';
import type { Story, StoryDefault } from '@ladle/react';
import { TraceTable } from '@/shared/ui/apm/trace-table';
import type { TraceStatus, TraceSummary } from '@/shared/api/types';

export default {
  title: 'APM / TraceTable',
} satisfies StoryDefault;

/**
 * Trace explorer — the product's entry screen. Filter by service/operation or
 * status, sort by duration / spans / start (click the header), click a row to
 * open the trace. The duration bar is relative to the slowest trace in view.
 */

const MIN = 60_000;
const now = Date.now();

const traces: TraceSummary[] = [
  { traceId: 'a1b2c3d4e5f60718', rootService: 'checkout-api', rootOperation: 'POST /checkout', startTime: now - 2 * MIN, durationMs: 1247, status: 'error', spanCount: 23, services: ['checkout-api', 'postgres', 'stripe', 'redis', 'kafka'] },
  { traceId: '9f8e7d6c5b4a3210', rootService: 'feed-svc', rootOperation: 'GET /feed', startTime: now - 5 * MIN, durationMs: 280, status: 'ok', spanCount: 9, services: ['feed-svc', 'postgres', 'redis'] },
  { traceId: '01234abcd5678ef9', rootService: 'search-svc', rootOperation: 'POST /search', startTime: now - 7 * MIN, durationMs: 2410, status: 'ok', spanCount: 17, services: ['search-svc', 'elastic', 'redis'] },
  { traceId: 'deadbeefcafe1234', rootService: 'user-svc', rootOperation: 'GET /users/u_42', startTime: now - 9 * MIN, durationMs: 145, status: 'error', spanCount: 6, services: ['user-svc', 'postgres'] },
  { traceId: 'feedface00112233', rootService: 'checkout-api', rootOperation: 'POST /cart/add', startTime: now - 12 * MIN, durationMs: 88, status: 'ok', spanCount: 5, services: ['checkout-api', 'redis'] },
  { traceId: 'aabbccddeeff0011', rootService: 'payment-svc', rootOperation: 'POST /charge', startTime: now - 15 * MIN, durationMs: 980, status: 'ok', spanCount: 14, services: ['payment-svc', 'stripe', 'postgres'] },
  { traceId: '5566778899aabbcc', rootService: 'notify-svc', rootOperation: 'publish email.send', startTime: now - 18 * MIN, durationMs: 42, status: 'unset', spanCount: 3, services: ['notify-svc', 'kafka'] },
  { traceId: '1122334455667788', rootService: 'inventory-svc', rootOperation: 'PUT /inventory', startTime: now - 24 * MIN, durationMs: 320, status: 'ok', spanCount: 8, services: ['inventory-svc', 'postgres'] },
  { traceId: 'c0ffee0011223344', rootService: 'gateway', rootOperation: 'GET /api/v2/health', startTime: now - 33 * MIN, durationMs: 12, status: 'ok', spanCount: 2, services: ['gateway'] },
  { traceId: 'abcabcabc1231231', rootService: 'search-svc', rootOperation: 'POST /reindex', startTime: now - 46 * MIN, durationMs: 5400, status: 'error', spanCount: 31, services: ['search-svc', 'elastic', 'postgres', 'kafka'] },
  { traceId: '778899aabbccddee', rootService: 'checkout-api', rootOperation: 'GET /orders/o_88', startTime: now - 61 * MIN, durationMs: 210, status: 'ok', spanCount: 7, services: ['checkout-api', 'postgres'] },
  { traceId: '00aa11bb22cc33dd', rootService: 'user-svc', rootOperation: 'POST /login', startTime: now - 92 * MIN, durationMs: 640, status: 'ok', spanCount: 10, services: ['user-svc', 'postgres', 'redis'] },
];

export const Default: Story = () => {
  const [opened, setOpened] = useState<string | null>(null);
  return (
    <div className="p-6">
      {opened && (
        <p className="mb-2 text-xs text-muted-foreground">
          Opened trace: <code className="font-mono text-foreground">{opened}</code>
        </p>
      )}
      <TraceTable traces={traces} onTraceClick={(t) => setOpened(t.traceId)} />
    </div>
  );
};

/** No results — the empty state (differs for zero-in-range vs filtered-out). */
export const Empty: Story = () => (
  <div className="p-6">
    <TraceTable traces={[]} />
  </div>
);

/** 30 traces — density + sorting/filtering at scale. */
export const ManyTraces: Story = () => {
  const services = ['gateway', 'checkout-api', 'user-svc', 'feed-svc', 'search-svc', 'payment-svc', 'inventory-svc', 'notify-svc', 'postgres', 'redis', 'stripe', 'kafka', 'elastic'];
  const ops = ['GET /feed', 'POST /checkout', 'GET /users', 'POST /search', 'PUT /inventory', 'POST /charge', 'publish event', 'GET /health'];
  const durations = [12, 88, 145, 210, 320, 640, 980, 1247, 2410, 5400];
  const many: TraceSummary[] = Array.from({ length: 30 }, (_, i) => {
    const status: TraceStatus = i % 7 === 0 ? 'error' : i % 11 === 0 ? 'unset' : 'ok';
    const count = 2 + (i % 5);
    return {
      traceId: (i.toString(16).padStart(2, '0') + 'f3c9a1b2c4d5e6').slice(0, 16),
      rootService: services[i % 8],
      rootOperation: ops[i % ops.length],
      startTime: now - (i + 1) * 3 * MIN,
      durationMs: durations[i % durations.length],
      status,
      spanCount: 2 + (i % 20),
      services: services.slice(i % 5, (i % 5) + count),
    };
  });
  return (
    <div className="p-6">
      <TraceTable traces={many} />
    </div>
  );
};
