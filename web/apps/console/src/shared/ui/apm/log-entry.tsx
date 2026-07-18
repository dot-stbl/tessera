import { useState } from 'react';
import { cn } from '@/shared/lib/utils';
import { LogLevel, parseLogLevel, type LogLevel as LogLevelType } from './log-level';
import { TimeFormat } from './time-format';

export interface LogEntryProps {
  /** Unix epoch milliseconds. */
  timestamp: number;
  /** Log severity. Either explicit level or raw string to parse. */
  level?: LogLevelType | string;
  /** Service that produced the log. */
  service?: string;
  /** Trace ID (rendered as TraceId). */
  traceId?: string;
  /** Span ID (rendered as TraceId). */
  spanId?: string;
  /** Log message body. */
  message: string;
  /** Structured fields attached to the log entry. */
  fields?: Record<string, string | number | boolean | undefined>;
  /** Whether to show structured fields by default. Default false. */
  defaultExpanded?: boolean;
  className?: string;
}

/**
 * Single log entry row. Level chip + timestamp + service + message + expandable
 * structured fields. The level chip color follows OTel/VictoriaLogs conventions.
 *
 * Usage:
 *   <LogEntry
 *     timestamp={1721337600400}
 *     level="ERROR"
 *     service="checkout-api"
 *     traceId="abc123def456"
 *     message="failed to charge card"
 *     fields={{ 'error.kind': 'StripeError', 'user.id': 'u_42' }}
 *   />
 */
export function LogEntry({
  timestamp,
  level,
  service,
  traceId,
  spanId,
  message,
  fields,
  defaultExpanded = false,
  className,
}: LogEntryProps) {
  const [expanded, setExpanded] = useState(defaultExpanded);
  const parsedLevel = typeof level === 'string' ? parseLogLevel(level) : level;
  const hasFields = fields && Object.keys(fields).length > 0;

  return (
    <div className={cn('log-entry', parsedLevel && `log-entry-${parsedLevel}`, className)}>
      <div className="log-entry-row">
        <TimeFormat ms={timestamp} format="time" className="log-time" />
        {parsedLevel && <LogLevel level={parsedLevel} />}
        {service && <span className="log-service">{service}</span>}
        <span className="log-message">{message}</span>
        {hasFields && (
          <button
            className="log-expand"
            onClick={() => setExpanded(!expanded)}
            aria-label={expanded ? 'Collapse fields' : 'Expand fields'}
          >
            {expanded ? '▼' : '▶'}
          </button>
        )}
      </div>
      {expanded && hasFields && (
        <dl className="log-fields">
          {traceId && (
            <>
              <dt>trace.id</dt>
              <dd>{traceId}</dd>
            </>
          )}
          {spanId && (
            <>
              <dt>span.id</dt>
              <dd>{spanId}</dd>
            </>
          )}
          {Object.entries(fields).map(([key, value]) => (
            <div key={key} className="log-field">
              <dt>{key}</dt>
              <dd>{value === undefined ? '—' : String(value)}</dd>
            </div>
          ))}
        </dl>
      )}
    </div>
  );
}