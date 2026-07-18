import { type ReactNode } from 'react';
import { cn } from '@/shared/lib/utils';

export type LogLevel = 'trace' | 'debug' | 'info' | 'warn' | 'error' | 'fatal';

export interface LogLevelProps {
  level: LogLevel;
  children?: ReactNode;
  className?: string;
}

/**
 * Compact pill showing log severity. Maps directly to OpenTelemetry log
 * severity levels (and VictoriaLogs _msg field when extracted).
 *
 * Usage:
 *   <LogLevel level="error">ERROR</LogLevel>
 *   <LogLevel level="warn" />  // auto-renders the label
 */
export function LogLevel({ level, children, className }: LogLevelProps) {
  return (
    <span className={cn('log-level', `log-level-${level}`, className)}>
      {children ?? level.toUpperCase()}
    </span>
  );
}

/**
 * Format an arbitrary string into a recognized LogLevel. Used when parsing
 * log messages that arrive with varying case ('error', 'ERROR', 'Error').
 */
export function parseLogLevel(raw: string | undefined | null): LogLevel | undefined {
  if (!raw) return undefined;
  const lower = raw.toLowerCase();
  if (lower === 'trace' || lower === 'debug' || lower === 'info' ||
      lower === 'warn' || lower === 'warning' ||
      lower === 'error' || lower === 'fatal' || lower === 'critical') {
    if (lower === 'warning') return 'warn';
    if (lower === 'critical') return 'fatal';
    return lower as LogLevel;
  }
  return undefined;
}