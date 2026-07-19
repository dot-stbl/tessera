import "@/index.css";
import type { Story, StoryDefault } from '@ladle/react';
import { LogEntry } from '@/shared/ui/apm/log-entry';

export default {
  title: 'APM / Log entry',
} satisfies StoryDefault;

/**
 * Single log line. Combines time, severity, service, optional
 * trace/span IDs and a message body. Expand button reveals
 * structured fields (KV pairs) below the row.
 */

const sampleFields = {
  'trace.id': 'abc123def4567890abcdef1234567890',
  'span.id': 'span_001',
  'error.kind': 'StripeError',
  'error.message': 'card_declined',
  'http.status_code': '402',
  'user.id': 'u_42',
};

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
      fields={sampleFields}
      defaultExpanded
    />
  </div>
);
