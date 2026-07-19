import type { Story, StoryDefault } from '@ladle/react';
import { LogEntry } from '@/shared/ui/apm/log-entry';

export default {
  title: 'APM / Log entry',
} satisfies StoryDefault;

/**
 * Single log line in a log viewer. Combines timestamp, severity, optional
 * trace/span IDs, service, message body, and a collapsible panel of
 * structured fields. Auto-detects level from raw string ('ERROR', 'warn').
 */

const NOW = Date.now();

export const Basic: Story = () => (
  <div className="p-6">
    <LogEntry
      timestamp={NOW}
      level="info"
      service="checkout-api"
      message="accepted checkout request"
    />
  </div>
);

export const AllLevels: Story = () => (
  <div className="p-6">
    <LogEntry timestamp={NOW - 1000} level="info"  service="checkout-api" message="accepted request" />
    <LogEntry timestamp={NOW - 500}  level="warn"  service="postgres"     message="connection pool 80% used" />
    <LogEntry timestamp={NOW - 100}  level="error" service="stripe"       message="card_declined" fields={{
      'trace.id': 'abc123def4567890abcdef1234567890',
      'span.id': 'span_001',
      'error.kind': 'StripeError',
    }} />
  </div>
);

export const WithTraceSpan: Story = () => (
  <div className="p-6">
    <LogEntry
      timestamp={NOW}
      level="info"
      service="checkout-api"
      message="request handled"
      traceId="abc123def4567890abcdef1234567890"
      spanId="span_001"
    />
  </div>
);

export const WithFields: Story = () => (
  <div className="p-6">
    <LogEntry
      timestamp={NOW}
      level="error"
      service="stripe"
      message="card_declined"
      fields={{
        'trace.id': 'abc123def4567890abcdef1234567890',
        'span.id': 'span_001',
        'error.kind': 'StripeError',
        'error.message': 'card_declined',
        'http.status_code': '402',
        'user.id': 'u_42',
        'order.id': 'ord_2024_abc',
      }}
      defaultExpanded
    />
  </div>
);

/** Real-world scenario: stack of log lines as they appear in a log viewer. */
export const InList: Story = () => (
  <div className="p-6 max-w-3xl">
    <LogEntry timestamp={NOW - 5000} level="info"  service="api-gateway"  message="GET /cart 200 142ms" />
    <LogEntry timestamp={NOW - 4200} level="info"  service="cart-svc"     message="cache hit: 2 items" />
    <LogEntry timestamp={NOW - 4100} level="info"  service="cart-svc"     message="GET /items 200 18ms" />
    <LogEntry timestamp={NOW - 3800} level="info"  service="checkout-api" message="POST /checkout" />
    <LogEntry timestamp={NOW - 3500} level="warn"  service="postgres"     message="slow query: 845ms (threshold 500ms)" />
    <LogEntry timestamp={NOW - 2700} level="error" service="stripe"       message="card_declined" fields={{ 'trace.id': 'abc123def4567890abcdef1234567890', 'error.kind': 'StripeCardError' }} />
    <LogEntry timestamp={NOW - 1200} level="warn"  service="checkout-api" message="retrying with idempotency-key" />
    <LogEntry timestamp={NOW - 300}  level="info"  service="checkout-api" message="checkout completed" />
  </div>
);

/** Long-form message wrapping test. */
export const LongMessage: Story = () => (
  <div className="p-6 max-w-3xl">
    <LogEntry
      timestamp={NOW}
      level="warn"
      service="batch-processor"
      message="batch job partially failed: 47/120 items processed successfully, 73 items failed with 'connection reset by peer' from upstream service; will retry with exponential backoff starting at 2s, max delay 60s, total retry budget 5 minutes before alerting on-call"
      fields={{
        'job.id': 'batch_2024_07_19_142',
        'processed': '47',
        'failed': '73',
        'retry.count': '0',
      }}
    />
  </div>
);

/** Multi-line message with embedded newlines. */
export const Multiline: Story = () => (
  <div className="p-6 max-w-3xl">
    <LogEntry
      timestamp={NOW}
      level="info"
      service="migration-runner"
      message={`migration 0042_add_users_email_index completed\n  → added index users_email_idx\n  → analyzed table users (1.2M rows)\n  → took 2.3s`}
      fields={{ 'migration.id': '0042', 'migration.duration_ms': '2300' }}
      defaultExpanded
    />
  </div>
);

/**
 * Many structured fields (10+). Used for error trace detail where every
 * span attribute becomes a key/value pair.
 */
export const ManyFields: Story = () => (
  <div className="p-6 max-w-3xl">
    <LogEntry
      timestamp={NOW}
      level="error"
      service="payment-gateway"
      message="3DS authentication failed"
      fields={{
        'trace.id': 'fedcba9876543210fedcba9876543210',
        'span.id': 'span_3ds_0042',
        'http.method': 'POST',
        'http.url': 'https://api.stripe.com/v1/three_d_secure',
        'http.status_code': '402',
        'error.kind': 'StripeAuthenticationError',
        'error.code': 'card_declined',
        'error.decline_code': 'generic_decline',
        'merchant.id': 'merch_42',
        'user.id': 'u_abc',
        'order.amount': '4200',
        'order.currency': 'USD',
        'card.brand': 'visa',
        'card.last4': '4242',
      }}
      defaultExpanded
    />
  </div>
);

/**
 * Parse level from raw string — `parseLogLevel` is exported and runs on
 * each `level` prop that isn't one of the strict six levels. Useful when
 * log sources emit varying case ('error', 'ERROR', 'Warning').
 */
export const ParseLevelFromRaw: Story = () => (
  <div className="p-6 max-w-xl">
    <LogEntry timestamp={NOW} level="ERROR"          service="svc-a" message="auto-rendered as upper-case" />
    <LogEntry timestamp={NOW} level="error"          service="svc-a" message="auto-rendered as lower-case" />
    <LogEntry timestamp={NOW} level="Warning"        service="svc-b" message="normalized to warn" />
    <LogEntry timestamp={NOW} level="critical"       service="svc-c" message="unknown level — falls back to default" />
    <LogEntry timestamp={NOW} level={undefined}      service="svc-d" message="missing level — falls back to default" />
  </div>
);

/** Compact card view — log line in a side panel. */
export const InSidePanel: Story = () => (
  <div className="p-6 max-w-md">
    <div className="rounded-md border border-border bg-card p-4 space-y-3">
      <h3 className="text-sm font-semibold">Span detail: span_001</h3>
      <p className="text-xs text-muted-foreground">Last log line from this span:</p>
      <LogEntry
        timestamp={NOW}
        level="error"
        service="stripe"
        message="card_declined"
        defaultExpanded
        fields={{ 'trace.id': 'abc123def4567890abcdef1234567890', 'span.id': 'span_001' }}
      />
    </div>
  </div>
);