import type { Story, StoryDefault } from '@ladle/react';
import { TraceId } from '@/shared/ui/apm/trace-id';
import { LogEntry } from '@/shared/ui/apm/log-entry';
import { SpanRow } from '@/shared/ui/apm/span-row';

export default {
  title: 'APM / Trace ID',
} satisfies StoryDefault;

/**
 * Trace / span ID display. Truncated to `prefix` chars + ellipsis. Hover
 * shows the full ID in tooltip. Click copies the full ID to clipboard.
 */

const NOW = Date.now();

export const StandardLength: Story = () => (
  <div className="flex flex-col gap-2 p-6">
    <TraceId id="abc123def4567890abcdef1234567890" />
    <TraceId id="0123456789abcdef0123456789abcdef" />
    <TraceId id="fedcba9876543210fedcba9876543210" />
  </div>
);

export const ShortAndLong: Story = () => (
  <div className="flex flex-col gap-2 p-6">
    <TraceId id="abc123" />
    <TraceId id="abc123def456" />
    <TraceId id="abc123def4567890abcdef" />
    <TraceId id="abc123def4567890abcdef1234567890" />
    <TraceId id="abc123def4567890abcdef1234567890abcdef1234567890abcdef1234567890" />
  </div>
);

export const CustomPrefix: Story = () => (
  <div className="flex flex-col gap-2 p-6">
    <div className="text-xs text-muted-foreground">prefix: 8</div>
    <TraceId id="abc123def4567890abcdef1234567890" prefix={8} />
    <div className="text-xs text-muted-foreground">prefix: 20</div>
    <TraceId id="abc123def4567890abcdef1234567890" prefix={20} />
    <div className="text-xs text-muted-foreground">prefix: 32 (full)</div>
    <TraceId id="abc123def4567890abcdef1234567890" prefix={32} />
  </div>
);

/** In log line — appears next to message. */
export const InLogRow: Story = () => (
  <div className="p-6 max-w-3xl">
    <LogEntry
      timestamp={NOW}
      level="error"
      service="stripe"
      message="card_declined"
      traceId="abc123def4567890abcdef1234567890"
      spanId="span_3ds_0042"
    />
  </div>
);

/** In span row — appears next to service name. */
export const InSpanRow: Story = () => (
  <div className="p-6 max-w-3xl space-y-2">
    <SpanRow
      service="checkout-api"
      name="POST /checkout"
      startOffsetMs={0}
      durationMs={1247}
      traceDurationMs={1300}
    />
    <div className="ml-6 text-xs text-muted-foreground font-mono">
      trace: <TraceId id="abc123def4567890abcdef1234567890" />
    </div>
    <div className="ml-6 text-xs text-muted-foreground font-mono">
      parent span: <TraceId id="span_root_001" prefix={16} />
    </div>
  </div>
);

/**
 * Stack of trace IDs in a breadcrumbs row — typical when navigating
 * service → trace → span.
 */
export const Breadcrumbs: Story = () => (
  <div className="flex items-center gap-2 p-6 text-sm">
    <span className="text-muted-foreground">Service:</span>
    <span className="font-medium">checkout-api</span>
    <span className="text-muted-foreground">/</span>
    <span className="text-muted-foreground">trace:</span>
    <TraceId id="abc123def4567890abcdef1234567890" />
    <span className="text-muted-foreground">/</span>
    <span className="text-muted-foreground">span:</span>
    <TraceId id="span_3ds_0042" prefix={16} />
  </div>
);

/** Compact — many IDs in a list (e.g., trace search results). */
export const ManyInList: Story = () => (
  <div className="p-6 max-w-xl">
    <ul className="divide-y divide-border font-mono text-xs">
      {Array.from({ length: 6 }, (_, i) => (
        <li key={i} className="flex items-center gap-3 py-2">
          <TraceId id={`${i.toString(16).padStart(2, '0')}${'a'.repeat(30)}`} />
          <span className="text-muted-foreground">·</span>
          <span className="text-muted-foreground">{120 + i * 18}ms</span>
          <span className="text-muted-foreground">·</span>
          <span>{i % 3 === 0 ? 'checkout-api' : i % 3 === 1 ? 'postgres' : 'stripe'}</span>
        </li>
      ))}
    </ul>
  </div>
);

/** In a trace summary header — full-width with copy affordance. */
export const InTraceHeader: Story = () => (
  <div className="p-6 max-w-3xl">
    <div className="flex items-baseline gap-4 rounded-md border border-border bg-card p-4">
      <div className="flex flex-col">
        <span className="text-xs text-muted-foreground">Trace ID</span>
        <TraceId id="abc123def4567890abcdef1234567890" />
      </div>
      <div className="flex flex-col">
        <span className="text-xs text-muted-foreground">Root span</span>
        <span className="font-mono text-xs">POST /checkout</span>
      </div>
      <div className="flex flex-col">
        <span className="text-xs text-muted-foreground">Service</span>
        <span className="text-sm">checkout-api</span>
      </div>
      <div className="flex flex-col">
        <span className="text-xs text-muted-foreground">Duration</span>
        <span className="font-mono text-sm">1247ms</span>
      </div>
      <div className="flex flex-col">
        <span className="text-xs text-muted-foreground">Spans</span>
        <span className="text-sm">6</span>
      </div>
    </div>
  </div>
);