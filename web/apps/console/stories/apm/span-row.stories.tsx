import type { Story, StoryDefault } from '@ladle/react';
import { SpanRow } from '@/shared/ui/apm/span-row';

export default {
  title: 'APM / Span row',
} satisfies StoryDefault;

/**
 * Single span in the waterfall. Used by `<Waterfall>` for each row, but
 * can also be rendered standalone when showing partial trace detail.
 */

export const Standalone: Story = () => (
  <div className="p-6">
    <SpanRow
      service="checkout-api"
      name="POST /checkout"
      durationMs={1247}
      startOffsetMs={0}
      traceDurationMs={1300}
    />
  </div>
);

export const Statuses: Story = () => (
  <div className="flex flex-col gap-3 p-6">
    <SpanRow service="postgres" name="SELECT orders" durationMs={50}   startOffsetMs={0}   traceDurationMs={1300} status="ok" />
    <SpanRow service="stripe"   name="Charge"       durationMs={1180} startOffsetMs={100} traceDurationMs={1300} status="error" />
    <SpanRow service="redis"    name="GET cache"    durationMs={5}    startOffsetMs={200} traceDurationMs={1300} status="unset" />
  </div>
);

export const Depths: Story = () => (
  <div className="flex flex-col gap-2 p-6">
    <SpanRow service="api"     name="root"    durationMs={1247} startOffsetMs={0}   traceDurationMs={1300} depth={0} />
    <SpanRow service="db"      name="SELECT"  durationMs={50}   startOffsetMs={12}  traceDurationMs={1300} depth={1} />
    <SpanRow service="cache"   name="GET"     durationMs={5}    startOffsetMs={64}  traceDurationMs={1300} depth={2} />
    <SpanRow service="worker"  name="publish" durationMs={20}   startOffsetMs={80}  traceDurationMs={1300} depth={3} />
  </div>
);

/** Long service + name — tests truncation behavior. */
export const VeryLongNames: Story = () => (
  <div className="p-6 max-w-4xl">
    <SpanRow
      service="analytics-pipeline-transform-worker"
      name="process_batch_with_retry_backoff_and_dlq"
      durationMs={1240}
      startOffsetMs={12}
      traceDurationMs={2000}
      status="ok"
    />
    <SpanRow
      service="user-profile-svc-prod"
      name="GET /api/v2/tenants/{tenant_id}/users/{user_id}/profile/full"
      durationMs={85}
      startOffsetMs={120}
      traceDurationMs={2000}
      status="ok"
    />
  </div>
);

/** Stack of spans in a side panel — span detail view. */
export const InSidePanel: Story = () => {
  const spans = [
    { service: 'checkout-api', name: 'POST /checkout',                       durationMs: 1247, startOffsetMs: 0,    status: 'ok' as const },
    { service: 'auth',         name: 'verify_jwt',                          durationMs: 8,    startOffsetMs: 0,    status: 'ok' as const },
    { service: 'cart',         name: 'GET /cart:user_42',                   durationMs: 4,    startOffsetMs: 11,   status: 'ok' as const },
    { service: 'inventory',    name: 'SELECT stock:product_abc,store_main', durationMs: 220,  startOffsetMs: 25,   status: 'ok' as const },
    { service: 'pricing',      name: 'compute price:product_abc',          durationMs: 18,   startOffsetMs: 245,  status: 'ok' as const },
    { service: 'promo',        name: 'POST /promo/validate',                durationMs: 142,  startOffsetMs: 263,  status: 'ok' as const },
    { service: 'stripe',       name: 'POST /v1/charges',                    durationMs: 1240, startOffsetMs: 405,  status: 'error' as const },
    { service: 'stripe',       name: '3DS authenticate',                    durationMs: 380,  startOffsetMs: 405,  status: 'ok' as const },
    { service: 'postgres',     name: 'INSERT orders',                       durationMs: 220,  startOffsetMs: 1185, status: 'ok' as const },
  ];
  return (
    <div className="p-6 max-w-3xl">
      <h3 className="text-sm font-semibold mb-3">Spans in trace 7f3a</h3>
      <div className="rounded-md border border-border bg-card overflow-hidden">
        {spans.map((s, i) => (
          <div key={i} className="border-b border-border last:border-0">
            <SpanRow
              service={s.service}
              name={s.name}
              durationMs={s.durationMs}
              startOffsetMs={s.startOffsetMs}
              traceDurationMs={1300}
              status={s.status}
              isSelected={i === 6}
            />
          </div>
        ))}
      </div>
    </div>
  );
};