import type { Story, StoryDefault } from '@ladle/react';
import { StatusPill } from '@/shared/ui/primitives/status-pill';

export default {
  title: 'APM / Status pill',
} satisfies StoryDefault;

/**
 * Status pill — visual indicator for trace status, span status, health
 * checks, or generic state. Extends the Plexor pill with APM-specific
 * variants (slow, idle, running, pending).
 */
export const AllVariants: Story = () => (
  <div className="flex flex-col gap-3 p-6">
    <div className="flex flex-wrap items-center gap-3">
      <StatusPill variant="ok">OK</StatusPill>
      <StatusPill variant="err">ERROR</StatusPill>
      <StatusPill variant="warn">DEGRADED</StatusPill>
      <StatusPill variant="warn" className="pill-slow">SLOW 1.2s</StatusPill>
      <StatusPill variant="idle">UNSET</StatusPill>
      <StatusPill variant="running">RUNNING</StatusPill>
      <StatusPill variant="pending">PENDING</StatusPill>
      <StatusPill variant="info">INFO</StatusPill>
    </div>
  </div>
);

export const SizesAndDotless: Story = () => (
  <div className="flex flex-col gap-4 p-6">
    <div className="flex items-center gap-3">
      <StatusPill variant="ok">OK</StatusPill>
      <StatusPill variant="err" hideDot>ERROR</StatusPill>
      <StatusPill variant="warn" hideDot>1.2s</StatusPill>
      <StatusPill variant="warn" hideDot>WARN</StatusPill>
    </div>
    <div className="flex items-center gap-3">
      <StatusPill variant="ok" size="sm">OK</StatusPill>
      <StatusPill variant="ok" size="md">OK</StatusPill>
    </div>
  </div>
);

export const InContext: Story = () => (
  <div className="flex flex-col gap-2 p-6 font-mono text-xs">
    <div className="flex items-center gap-2">
      <StatusPill variant="ok">200</StatusPill>
      <span>checkout-api GET /cart → 124ms</span>
    </div>
    <div className="flex items-center gap-2">
      <StatusPill variant="err">500</StatusPill>
      <span>stripe POST /charge → 1247ms</span>
    </div>
    <div className="flex items-center gap-2">
      <StatusPill variant="warn">SLOW</StatusPill>
      <span>postgres SELECT → 4.2s (above 1s threshold)</span>
    </div>
    <div className="flex items-center gap-2">
      <StatusPill variant="idle">UNSET</StatusPill>
      <span>cache lookup — status not yet reported</span>
    </div>
  </div>
);