import type { Story, StoryDefault } from '@ladle/react';
import { StatusPill } from '@/shared/ui/primitives/status-pill';

export default {
  title: 'APM / Status pill',
} satisfies StoryDefault;

/**
 * Status pill — visual indicator for trace status, span status, health
 * checks. Extends the Plexor pill with APM-specific variants.
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

export const InLogLineContext: Story = () => (
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
      <StatusPill variant="warn" className="pill-slow">SLOW</StatusPill>
      <span>postgres SELECT → 4.2s (above 1s threshold)</span>
    </div>
    <div className="flex items-center gap-2">
      <StatusPill variant="idle">UNSET</StatusPill>
      <span>cache lookup — status not yet reported</span>
    </div>
  </div>
);

/** Density — 12-row trace list (real APM list view). */
export const DenseTraceList: Story = () => {
  const rows = [
    { trace: '7f3a', service: 'checkout-api', ms: 1247, status: 'ok' as const },
    { trace: '8c4d', service: 'cart-svc',     ms: 38,   status: 'ok' as const },
    { trace: '9e5f', service: 'pricing-svc',  ms: 1240, status: 'warn' as const },
    { trace: 'a1b2', service: 'stripe',       ms: 12_400, status: 'err' as const },
    { trace: 'b3c4', service: 'postgres',     ms: 220,  status: 'ok' as const },
    { trace: 'c5d6', service: 'redis',        ms: 4,    status: 'ok' as const },
    { trace: 'd7e8', service: 'auth-svc',     ms: 8,    status: 'ok' as const },
    { trace: 'f9a0', service: 'user-svc',     ms: 145,  status: 'ok' as const },
    { trace: 'b1c2', service: 'rec-svc',      ms: 12,   status: 'idle' as const },
    { trace: 'd3e4', service: 'feed-svc',     ms: 280,  status: 'ok' as const },
    { trace: 'f5a6', service: 'audit-svc',    ms: 22,   status: 'ok' as const },
    { trace: 'c7d8', service: 'notify-svc',   ms: 15,   status: 'idle' as const },
  ];
  return (
    <div className="p-6 max-w-3xl">
      <ul className="divide-y divide-border font-mono text-xs">
        {rows.map((r) => (
          <li key={r.trace} className="flex items-center gap-3 py-1.5">
            <span className="text-muted-foreground w-12">{r.trace}…</span>
            <span className="flex-1">{r.service}</span>
            <span className="w-16 text-right text-muted-foreground">{r.ms}ms</span>
            <StatusPill variant={r.status}>
              {r.status === 'ok' ? 'OK' : r.status === 'err' ? 'ERROR' : r.status === 'warn' ? 'WARN' : 'UNSET'}
            </StatusPill>
          </li>
        ))}
      </ul>
    </div>
  );
};

/** In card header — service health card. */
export const InCardHeader: Story = () => (
  <div className="p-6 max-w-3xl">
    <div className="rounded-md border border-border bg-card p-4 space-y-3">
      <div className="flex items-center justify-between">
        <div>
          <h3 className="text-sm font-semibold">checkout-api</h3>
          <p className="text-xs text-muted-foreground">3 instances · last check 12s ago</p>
        </div>
        <StatusPill variant="err">UNHEALTHY</StatusPill>
      </div>
      <div className="grid grid-cols-3 gap-3 text-xs">
        <div>
          <div className="text-muted-foreground">p99 latency</div>
          <div className="font-mono">1.2s</div>
        </div>
        <div>
          <div className="text-muted-foreground">Error rate</div>
          <div className="font-mono text-err-ink">14%</div>
        </div>
        <div>
          <div className="text-muted-foreground">RPS</div>
          <div className="font-mono">420</div>
        </div>
      </div>
    </div>
  </div>
);

/**
 * Service map legend — what each pill colour means.
 */
export const ServiceMapLegend: Story = () => (
  <div className="p-6 max-w-md space-y-3">
    <div className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
      Health legend
    </div>
    <div className="space-y-2">
      <div className="flex items-center gap-3">
        <StatusPill variant="ok">Healthy</StatusPill>
        <span className="text-xs text-muted-foreground">error rate &lt; 0.1%, p99 in SLO</span>
      </div>
      <div className="flex items-center gap-3">
        <StatusPill variant="warn">Degraded</StatusPill>
        <span className="text-xs text-muted-foreground">error rate 0.1–1%, p99 approaching SLO</span>
      </div>
      <div className="flex items-center gap-3">
        <StatusPill variant="err">Unhealthy</StatusPill>
        <span className="text-xs text-muted-foreground">error rate &gt; 1% or p99 over SLO</span>
      </div>
      <div className="flex items-center gap-3">
        <StatusPill variant="idle">Unknown</StatusPill>
        <span className="text-xs text-muted-foreground">no recent telemetry</span>
      </div>
    </div>
  </div>
);