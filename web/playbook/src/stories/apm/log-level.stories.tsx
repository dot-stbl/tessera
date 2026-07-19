import type { Story, StoryDefault } from '@ladle/react';
import { LogLevel } from '@/shared/ui/apm/log-level';

export default {
  title: 'APM / Log level',
} satisfies StoryDefault;

/**
 * Log level chip — compact pill showing log severity. Maps to OpenTelemetry
 * SeverityNumber and VictoriaLogs' `_stream` field. Six levels: TRACE /
 * DEBUG / INFO / WARN / ERROR / FATAL.
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

export const InLogLineContext: Story = () => (
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