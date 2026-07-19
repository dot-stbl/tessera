import type { Story, StoryDefault } from '@ladle/react';
import { LogLevel } from '@/shared/ui/apm/log-level';

export default {
  title: 'APM / Log level',
} satisfies StoryDefault;

/**
 * Compact pill showing log severity. Maps to OpenTelemetry SeverityNumber
 * and VictoriaLogs' `_stream` field.
 */

export const AllLevels: Story = () => (
  <div className="flex flex-col gap-3 p-6">
    <div className="flex flex-wrap items-center gap-2">
      <LogLevel level="trace">TRACE</LogLevel>
      <LogLevel level="debug">DEBUG</LogLevel>
      <LogLevel level="info">INFO</LogLevel>
      <LogLevel level="warn">WARN</LogLevel>
      <LogLevel level="error">ERROR</LogLevel>
      <LogLevel level="fatal">FATAL</LogLevel>
    </div>
    <div className="flex flex-wrap items-center gap-2">
      <LogLevel level="trace">trace</LogLevel>
      <LogLevel level="debug">debug</LogLevel>
      <LogLevel level="info">info</LogLevel>
      <LogLevel level="warn">warn</LogLevel>
      <LogLevel level="error">error</LogLevel>
      <LogLevel level="fatal">fatal</LogLevel>
    </div>
  </div>
);

export const InLogLine: Story = () => (
  <div className="flex flex-col gap-2 p-6 font-mono text-xs">
    <div className="flex items-center gap-2">
      <LogLevel level="info">INFO</LogLevel>
      <span>checkout-api accepted request</span>
    </div>
    <div className="flex items-center gap-2">
      <LogLevel level="warn">WARN</LogLevel>
      <span>connection pool 80% utilized</span>
    </div>
    <div className="flex items-center gap-2">
      <LogLevel level="error">ERROR</LogLevel>
      <span>stripe charge failed: card_declined</span>
    </div>
    <div className="flex items-center gap-2">
      <LogLevel level="fatal">FATAL</LogLevel>
      <span>postgres replica lost quorum</span>
    </div>
  </div>
);

/**
 * Distribution — counts per level (log viewer summary card).
 * Used in the log viewer's left filter rail.
 */
export const Distribution: Story = () => (
  <div className="p-6 max-w-sm">
    <div className="rounded-md border border-border bg-card p-4">
      <div className="text-sm font-semibold mb-3">Last 5 minutes</div>
      <div className="space-y-2">
        {([
          { level: 'fatal' as const, count: 1 },
          { level: 'error' as const, count: 14 },
          { level: 'warn'  as const, count: 87 },
          { level: 'info'  as const, count: 1240 },
          { level: 'debug' as const, count: 3210 },
          { level: 'trace' as const, count: 0 },
        ]).map(({ level, count }) => (
          <div key={level} className="flex items-center gap-3 text-xs">
            <LogLevel level={level}>{level}</LogLevel>
            <div className="flex-1">
              <div className="bg-surface-2 rounded-full h-2 overflow-hidden">
                <div
                  className="bg-accent h-full"
                  style={{ width: `${(count / 3210) * 100}%` }}
                />
              </div>
            </div>
            <div className="font-mono w-12 text-right">{count.toLocaleString()}</div>
          </div>
        ))}
      </div>
    </div>
  </div>
);

/** Filter chip in log viewer filter bar — toggleable, with count badge. */
export const FilterChip: Story = () => (
  <div className="p-6 max-w-2xl">
    <div className="flex flex-wrap gap-2">
      <button className="inline-flex items-center gap-1.5 rounded-full border border-input bg-background px-3 py-1 text-xs hover:bg-surface-2">
        <LogLevel level="error">error</LogLevel>
        <span className="rounded-full bg-err-soft px-1.5 text-err-ink font-mono text-[10px]">14</span>
      </button>
      <button className="inline-flex items-center gap-1.5 rounded-full border border-input bg-background px-3 py-1 text-xs hover:bg-surface-2">
        <LogLevel level="warn">warn</LogLevel>
        <span className="rounded-full bg-warn-soft px-1.5 text-warn-ink font-mono text-[10px]">87</span>
      </button>
      <button className="inline-flex items-center gap-1.5 rounded-full border border-accent bg-accent px-3 py-1 text-xs text-accent-foreground">
        <LogLevel level="info">info</LogLevel>
        <span className="rounded-full bg-accent-foreground/20 px-1.5 text-accent-foreground font-mono text-[10px]">1240</span>
      </button>
    </div>
    <p className="text-xs text-muted-foreground mt-3">
      Active filter: <code>info</code> is selected (blue accent). Other chips show
      counts in the right colour for the level.
    </p>
  </div>
);

/**
 * In a trace detail header — summary chip near the trace ID.
 */
export const InTraceHeader: Story = () => (
  <div className="p-6 max-w-3xl">
    <div className="flex items-baseline gap-3">
      <h2 className="text-lg font-semibold">Trace 7f3a</h2>
      <LogLevel level="error">errors: 1</LogLevel>
      <LogLevel level="warn">warns: 3</LogLevel>
    </div>
    <p className="text-xs text-muted-foreground mt-1">
      checkout-api · POST /checkout · 1247ms · 6 spans
    </p>
  </div>
);

/**
 * Compact counter badges — top-of-page severity overview.
 */
export const SeverityBadges: Story = () => (
  <div className="flex items-center gap-2 p-6">
    <div className="inline-flex items-center gap-1 rounded-md bg-err-soft px-2 py-0.5 text-xs font-medium text-err-ink">
      <LogLevel level="error">E</LogLevel>
      <span className="font-mono">14</span>
    </div>
    <div className="inline-flex items-center gap-1 rounded-md bg-warn-soft px-2 py-0.5 text-xs font-medium text-warn-ink">
      <LogLevel level="warn">W</LogLevel>
      <span className="font-mono">87</span>
    </div>
    <div className="inline-flex items-center gap-1 rounded-md bg-ok-soft px-2 py-0.5 text-xs font-medium text-ok-ink">
      <LogLevel level="ok">OK</LogLevel>
      <span className="font-mono">1240</span>
    </div>
  </div>
);