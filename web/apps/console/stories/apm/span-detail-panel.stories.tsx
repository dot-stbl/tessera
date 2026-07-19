import { useState } from 'react';
import type { Story, StoryDefault } from '@ladle/react';
import {
  SpanDetailPanel,
  type SpanDetailPanelProps,
} from '@/shared/ui/apm/span-detail-panel';
import { Waterfall, type WaterfallSpan } from '@/shared/ui/apm/waterfall';

export default {
  title: 'APM / SpanDetailPanel',
} satisfies StoryDefault;

/**
 * Span detail flyout — opens when a span is selected in the waterfall. Shows
 * status, service·operation, ids, timing (duration + self time), tags, and
 * events (exceptions highlighted in red).
 */

const errorSpan: SpanDetailPanelProps = {
  service: 'stripe',
  operation: 'api.call POST /v1/charges',
  spanId: '7f3c9a1b2c4d5e6f',
  durationMs: 880,
  selfTimeMs: 610,
  status: 'error',
  tags: {
    'http.method': 'POST',
    'http.url': 'https://api.stripe.com/v1/charges',
    'http.status_code': '502',
    'peer.service': 'stripe',
    'otel.status': 'ERROR — upstream returned 502',
  },
  events: [
    {
      offsetMs: 610,
      name: 'exception',
      attributes: {
        'exception.type': 'StripeError',
        'exception.message': 'gateway timeout while charging card •••• 4242',
      },
    },
  ],
};

export const ErrorSpan: Story = () => (
  <div className="max-w-sm p-6">
    <SpanDetailPanel {...errorSpan} />
  </div>
);

export const OkSpan: Story = () => (
  <div className="max-w-sm p-6">
    <SpanDetailPanel
      service="postgres"
      operation="SELECT orders"
      spanId="a1b2c3d4e5f60718"
      durationMs={140}
      selfTimeMs={96}
      status="ok"
      tags={{
        'db.system': 'postgresql',
        'db.statement': 'SELECT * FROM orders WHERE user_id = $1',
        'db.rows': 3,
      }}
    />
  </div>
);

/** No tags or events — just the header and timing. */
export const Minimal: Story = () => (
  <div className="max-w-sm p-6">
    <SpanDetailPanel service="redis" operation="GET session" durationMs={9} status="ok" />
  </div>
);

/** With a close button — clicking it hides the panel. */
export const WithClose: Story = () => {
  const [open, setOpen] = useState(true);
  return (
    <div className="max-w-sm p-6">
      {open ? (
        <SpanDetailPanel {...errorSpan} onClose={() => setOpen(false)} />
      ) : (
        <p className="text-sm text-muted-foreground">Panel closed.</p>
      )}
    </div>
  );
};

const traceSpans: WaterfallSpan[] = [
  { id: 'root', name: 'POST /checkout', service: 'checkout-api', startOffsetMs: 0, durationMs: 1247, status: 'ok', isCritical: true },
  { id: 'db', name: 'SELECT orders', service: 'postgres', startOffsetMs: 66, durationMs: 140, depth: 1, status: 'ok' },
  { id: 'charge', name: 'POST /charges', service: 'stripe', startOffsetMs: 215, durationMs: 985, depth: 1, status: 'error', isCritical: true },
  { id: 'kafka', name: 'publish order.placed', service: 'kafka', startOffsetMs: 1200, durationMs: 38, depth: 1, status: 'ok' },
];

const spanDetails: Record<string, SpanDetailPanelProps> = {
  root: {
    service: 'checkout-api',
    operation: 'POST /checkout',
    spanId: '0af1b2c3d4e5f607',
    durationMs: 1247,
    selfTimeMs: 84,
    status: 'ok',
    tags: { 'http.method': 'POST', 'http.route': '/checkout', 'http.status_code': '200' },
  },
  db: {
    service: 'postgres',
    operation: 'SELECT orders',
    spanId: 'a1b2c3d4e5f60718',
    durationMs: 140,
    selfTimeMs: 96,
    status: 'ok',
    tags: { 'db.system': 'postgresql', 'db.statement': 'SELECT * FROM orders WHERE user_id = $1', 'db.rows': 3 },
  },
  charge: { ...errorSpan, operation: 'POST /charges', durationMs: 985, selfTimeMs: 41 },
  kafka: {
    service: 'kafka',
    operation: 'publish order.placed',
    spanId: 'c9d8e7f6a5b4c3d2',
    durationMs: 38,
    selfTimeMs: 38,
    status: 'ok',
    tags: { 'messaging.system': 'kafka', 'messaging.destination': 'order.placed' },
  },
};

/** The full trace-detail flow: click a span in the waterfall → panel updates. */
export const InWaterfall: Story = () => {
  const [selectedId, setSelectedId] = useState<string | undefined>('charge');
  const selected = selectedId ? spanDetails[selectedId] : undefined;
  return (
    <div className="flex gap-4 p-6">
      <div className="min-w-0 flex-1">
        <Waterfall
          spans={traceSpans}
          totalDurationMs={1300}
          selectedSpanId={selectedId}
          onSpanClick={(s) => setSelectedId(s.id)}
        />
      </div>
      <div className="w-80 shrink-0">
        {selected ? (
          <SpanDetailPanel {...selected} onClose={() => setSelectedId(undefined)} />
        ) : (
          <p className="p-4 text-sm text-muted-foreground">Select a span to see its details.</p>
        )}
      </div>
    </div>
  );
};
