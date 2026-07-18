/**
 * Tessera API types — mirror the backend C# records.
 * Source of truth: .agents/docs/modules.md (per-module contracts) and
 * .agents/docs/dashboard-schema.md.
 *
 * All times are UTC unix milliseconds on the wire.
 */

export type LogLevel = 'trace' | 'debug' | 'info' | 'warn' | 'error' | 'fatal';

export type TraceStatus = 'ok' | 'error' | 'unset';

export interface TimeRange {
  startUnixMs: number;
  endUnixMs: number;
}

export interface TraceSummary {
  traceId: string;
  rootService: string;
  rootOperation: string;
  startTime: number;
  durationMs: number;
  status: TraceStatus;
  spanCount: number;
  services: string[];
}

export interface Span {
  spanId: string;
  parentSpanId: string | null;
  service: string;
  operation: string;
  startTime: number;
  durationMs: number;
  status: TraceStatus;
  tags: Record<string, string>;
  events: SpanEvent[];
}

export interface SpanEvent {
  time: number;
  name: string;
  attributes: Record<string, string>;
}

export interface TraceDetail {
  traceId: string;
  rootService: string;
  rootOperation: string;
  startTime: number;
  durationMs: number;
  status: TraceStatus;
  spans: Span[];
}

export interface LogEntry {
  timestamp: number;
  level: LogLevel | string;
  service?: string;
  traceId?: string;
  spanId?: string;
  message: string;
  fields?: Record<string, string | number | boolean | undefined>;
}

export interface ServiceOperation {
  name: string;
  count: number;
}

export interface ServiceSummary {
  name: string;
  spanCount: number;
  errorCount: number;
  operations: ServiceOperation[];
}

export interface ListTracesRequest {
  service?: string;
  operation?: string;
  startUnixMs: number;
  endUnixMs: number;
  minDurationMs?: number;
  maxDurationMs?: number;
  limit?: number;
}

export interface ListTracesResponse {
  items: TraceSummary[];
  cursor?: string;
  hasMore: boolean;
}

export interface GetTraceRequest {
  traceId: string;
}

export interface ListLogsRequest {
  traceId?: string;
  query?: string;       // LogsQL expression (overrides traceId if both)
  startUnixMs?: number;
  endUnixMs?: number;
  limit?: number;
}

export interface ListLogsResponse {
  entries: LogEntry[];
  total: number;
}

export interface ListServicesRequest {
  startUnixMs: number;
  endUnixMs: number;
}

export interface ListServicesResponse {
  items: ServiceSummary[];
}

export interface HealthResponse {
  status: 'healthy' | 'degraded' | 'unhealthy';
  backends: {
    vt: { status: 'ok' | 'error'; latencyMs?: number; error?: string };
    vl: { status: 'ok' | 'error'; latencyMs?: number; error?: string };
    vm: { status: 'ok' | 'error'; latencyMs?: number; error?: string };
  };
}