import "@/index.css";
import type { Story, StoryDefault } from '@ladle/react';
import { TraceId } from '@/shared/ui/apm/trace-id';

export default {
  title: 'APM / Trace ID',
} satisfies StoryDefault;

/**
 * Trace / span ID display. Truncated to `prefix` chars (default 12) with
 * an ellipsis. Hover shows the full ID in a tooltip. Click copies the
 * full ID to the clipboard; the component shows brief "copied" feedback.
 */

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
