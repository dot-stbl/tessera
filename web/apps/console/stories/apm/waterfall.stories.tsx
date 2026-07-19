import type { Story, StoryDefault } from '@ladle/react';
import { Waterfall, type WaterfallSpan } from '@/shared/ui/apm/waterfall';

export default {
  title: 'APM / Waterfall',
} satisfies StoryDefault;

/**
 * Trace waterfall — Jaeger-style timeline. Bar position = `(startOffsetMs /
 * totalDurationMs)`, bar width = `(durationMs / totalDurationMs)`. Depth
 * renders as indentation; status colours the bar.
 */

const sampleSpans: WaterfallSpan[] = [
  { id: 's1', name: 'POST /checkout', service: 'checkout-api', startOffsetMs: 0,    durationMs: 1247, status: 'ok' },
  { id: 's2', name: 'SELECT orders',  service: 'postgres',     startOffsetMs: 12,   durationMs: 50,   depth: 1, status: 'ok' },
  { id: 's3', name: 'SELECT items',   service: 'postgres',     startOffsetMs: 64,   durationMs: 23,   depth: 2, status: 'ok' },
  { id: 's4', name: 'INSERT audit',   service: 'postgres',     startOffsetMs: 90,   durationMs: 12,   depth: 2, status: 'ok' },
  { id: 's5', name: 'Charge',         service: 'stripe',       startOffsetMs: 95,   durationMs: 1180, depth: 1, status: 'error' },
  { id: 's6', name: 'Refund (idempotent)', service: 'stripe',  startOffsetMs: 1180, durationMs: 12,   depth: 2, status: 'unset' },
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

/**
 * Real-world checkout trace with 6+ depths (api → checkout → db → cache
 * → worker → external). Stress-tests indentation + bar layout.
 */
export const RealisticCheckout: Story = () => {
  const spans: WaterfallSpan[] = [
    { id: 'a', name: 'POST /api/v2/checkout', service: 'api-gateway', startOffsetMs: 0,    durationMs: 1820, status: 'ok' },
    { id: 'b', name: 'auth.verify_jwt',       service: 'api-gateway', startOffsetMs: 0,    durationMs: 8,    depth: 1, status: 'ok' },
    { id: 'c', name: 'route /checkout',      service: 'api-gateway', startOffsetMs: 8,    durationMs: 3,    depth: 1, status: 'ok' },
    { id: 'd', name: 'POST /checkout',       service: 'checkout-api', startOffsetMs: 11,   durationMs: 1799, depth: 1, status: 'ok' },
    { id: 'e', name: 'validate cart',        service: 'checkout-api', startOffsetMs: 11,   durationMs: 14,   depth: 2, status: 'ok' },
    { id: 'f', name: 'GET cart:user_42',     service: 'redis',        startOffsetMs: 25,   durationMs: 4,    depth: 3, status: 'ok' },
    { id: 'g', name: 'GET user:user_42',     service: 'redis',        startOffsetMs: 30,   durationMs: 3,    depth: 3, status: 'ok' },
    { id: 'h', name: 'compute totals',      service: 'checkout-api', startOffsetMs: 35,   durationMs: 18,   depth: 2, status: 'ok' },
    { id: 'i', name: 'apply promo',          service: 'checkout-api', startOffsetMs: 53,   durationMs: 142,  depth: 2, status: 'ok' },
    { id: 'j', name: 'POST /promo/validate', service: 'promo-svc',    startOffsetMs: 60,   durationMs: 130,  depth: 3, status: 'ok' },
    { id: 'k', name: 'process payment',     service: 'checkout-api', startOffsetMs: 195,  durationMs: 1610, depth: 2, status: 'ok' },
    { id: 'l', name: 'INSERT orders',        service: 'postgres',     startOffsetMs: 210,  durationMs: 220,  depth: 3, status: 'ok' },
    { id: 'm', name: 'SELECT order_items',   service: 'postgres',     startOffsetMs: 430,  durationMs: 18,   depth: 3, status: 'ok' },
    { id: 'n', name: 'UPDATE inventory',     service: 'postgres',     startOffsetMs: 448,  durationMs: 95,   depth: 3, status: 'ok' },
    { id: 'o', name: 'POST /v1/charges',     service: 'stripe',       startOffsetMs: 543,  durationMs: 1240, depth: 3, status: 'ok' },
    { id: 'p', name: '3DS authenticate',     service: 'stripe',       startOffsetMs: 543,  durationMs: 380,  depth: 4, status: 'ok' },
    { id: 'q', name: 'POST /3ds/verify',     service: '3ds-svc',      startOffsetMs: 555,  durationMs: 360,  depth: 5, status: 'ok' },
    { id: 'r', name: 'charge confirmed',     service: 'stripe',       startOffsetMs: 923,  durationMs: 8,    depth: 4, status: 'ok' },
    { id: 's', name: 'POST /audit',          service: 'audit-svc',    startOffsetMs: 1783, durationMs: 22,   depth: 3, status: 'ok' },
    { id: 't', name: 'POST /notify',         service: 'notify-svc',   startOffsetMs: 1805, durationMs: 15,   depth: 3, status: 'unset' },
  ];
  return (
    <div className="p-6">
      <Waterfall spans={spans} totalDurationMs={1820} />
    </div>
  );
};

/** Span name longer than the label column — tests overflow handling. */
export const VeryLongNames: Story = () => {
  const spans: WaterfallSpan[] = [
    { id: 'a', name: 'GET /api/v2/tenants/42/workspaces/abc-123-def-456/projects/cool-project-7/datasets/metrics-2024-07/query', service: 'analytics-svc', startOffsetMs: 0, durationMs: 480, status: 'ok' },
    { id: 'b', name: 'redis.lookup({"key":"tenant:42:workspace:abc:preferences:user_42","type":"HASH","encoding":"UTF-8"})', service: 'redis', startOffsetMs: 12, durationMs: 8, depth: 1, status: 'ok' },
    { id: 'c', name: 'sql: SELECT users.email, users.display_name, workspaces.preferences FROM users JOIN workspaces ON ... WHERE users.id = $1 AND workspaces.tenant_id = $2', service: 'postgres', startOffsetMs: 24, durationMs: 220, depth: 1, status: 'ok' },
  ];
  return (
    <div className="p-6 max-w-4xl">
      <Waterfall spans={spans} totalDurationMs={500} />
    </div>
  );
};

/**
 * 50-span synthetic trace — stress-tests render performance and row count.
 */
export const ManySpans: Story = () => {
  const services = ['api-gw', 'auth', 'cart', 'inventory', 'pricing', 'payment', 'audit', 'notify'];
  const ops = ['GET /items', 'POST /cart', 'PUT /qty', 'SELECT items', 'INSERT audit', 'PUBLISH event'];
  const spans: WaterfallSpan[] = Array.from({ length: 50 }, (_, i) => ({
    id: `s${i}`,
    name: ops[i % ops.length],
    service: services[i % services.length],
    startOffsetMs: i * 50,
    durationMs: 5 + (i % 11) * 20,
    depth: Math.min(i % 5, 3),
    status: i % 13 === 0 ? 'error' : i % 17 === 0 ? 'unset' : 'ok',
  }));
  return (
    <div className="p-6">
      <Waterfall spans={spans} totalDurationMs={2600} />
    </div>
  );
};

/** Mixed statuses — realistic APM view with errors scattered among healthy spans. */
export const MixedStatuses: Story = () => {
  const spans: WaterfallSpan[] = [
    { id: '1', name: 'GET /feed',                  service: 'feed-svc',       startOffsetMs: 0,    durationMs: 280, status: 'ok' },
    { id: '2', name: 'cache lookup',               service: 'feed-svc',       startOffsetMs: 2,    durationMs: 4,   depth: 1, status: 'ok' },
    { id: '3', name: 'SELECT posts',               service: 'postgres',       startOffsetMs: 6,    durationMs: 90,  depth: 1, status: 'ok' },
    { id: '4', name: 'rank posts',                 service: 'feed-svc',       startOffsetMs: 96,   durationMs: 24,  depth: 1, status: 'ok' },
    { id: '5', name: 'GET /users/u_42/profile',    service: 'user-svc',       startOffsetMs: 120,  durationMs: 145, status: 'error' },
    { id: '6', name: 'cache lookup',               service: 'user-svc',       startOffsetMs: 122,  durationMs: 3,   depth: 1, status: 'ok' },
    { id: '7', name: 'GET /db/u_42',               service: 'postgres',       startOffsetMs: 125,  durationMs: 140, depth: 1, status: 'error' },
    { id: '8', name: 'GET /recommendations/u_42',  service: 'rec-svc',        startOffsetMs: 265,  durationMs: 12,  status: 'unset' },
    { id: '9', name: 'mq publish',                 service: 'feed-svc',       startOffsetMs: 277,  durationMs: 3,   depth: 1, status: 'ok' },
  ];
  return (
    <div className="p-6">
      <Waterfall spans={spans} totalDurationMs={280} />
    </div>
  );
};

/** With selected span — visual highlight. */
export const WithSelection: Story = () => {
  const spans: WaterfallSpan[] = [
    ...sampleSpans,
  ];
  return (
    <div className="p-6">
      <p className="text-xs text-muted-foreground mb-2">
        selectedSpanId="s5" (Charge) — bar highlighted via :where(.selected).
      </p>
      <Waterfall spans={spans} totalDurationMs={1300} selectedSpanId="s5" />
    </div>
  );
};