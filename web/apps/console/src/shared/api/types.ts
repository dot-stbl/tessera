/**
 * Ergonomic aliases over the generated OpenAPI schema.
 *
 * Nothing here describes the API — `schema.gen.ts` does, and it is generated
 * from the document the backend serves (`bun run codegen`). This file only gives
 * the generated component schemas short names so call sites read as
 * `TraceSummary` rather than `components['schemas']['TraceSummary']`.
 *
 * The previous version of this file hand-transcribed the C# records from prose
 * docs, and every one of them had drifted: routes were missing the `v1` segment,
 * statuses were compared against lowercase literals the wire never sent, the
 * health response carried a Victoria-shaped `{vt, vl, vm}` body that no endpoint
 * has ever returned, and the trace response was missing the correlated logs the
 * backend already computes. Do not reintroduce hand-written mirrors — add an
 * alias here instead.
 */
import type { components } from './schema.gen';

type Schemas = components['schemas'];

// ─── Identifiers ───────────────────────────────────────────────────────
// Plain strings on the wire; the C# side wraps them in TraceId/SpanId records
// purely to stop the two being mixed up in-process.
export type TraceId = Schemas['TraceId'];
export type SpanId = Schemas['SpanId'];

// ─── Enums ─────────────────────────────────────────────────────────────
export type TraceStatus = Schemas['TraceStatus'];
export type LogLevel = Schemas['LogLevel'];
export type SpanKind = Schemas['SpanKind'];
export type HealthStatus = Schemas['HealthStatus'];

/**
 * How complete the request view is. The backend answers 200 when it has spans or
 * logs, and says which — the UI is expected to degrade rather than show an error
 * when only half the picture exists.
 */
export type RequestViewMode = Schemas['RequestViewMode'];

// ─── Traces ────────────────────────────────────────────────────────────
export type TraceSummary = Schemas['TraceSummary'];
export type TraceDetail = Schemas['TraceDetail'];
export type Span = Schemas['Span'];
export type SpanEvent = Schemas['SpanEvent'];
export type Resource = Schemas['Resource'];
export type TraceExceptionSummary = Schemas['TraceExceptionSummary'];

/**
 * The trace-detail payload. Carries the span tree *and* the logs already
 * correlated to it, plus {@link LogMarker}s positioned on the trace timeline —
 * so rendering "logs for this span" needs no second request.
 */
export type GetTraceResponse = Schemas['GetTraceResponse'];

/** A log pinned to a point on the trace timeline, optionally to one span. */
export type LogMarker = Schemas['LogMarker'];

// ─── Logs ──────────────────────────────────────────────────────────────
export type LogEntry = Schemas['LogEntry'];

// ─── Discovery / health / errors ───────────────────────────────────────
export type ServiceSummary = Schemas['ServiceSummary'];
export type ServiceOperation = Schemas['ServiceOperation'];
export type HealthResponse = Schemas['HealthResponse'];
export type ErrorGroupSummary = Schemas['ErrorGroupSummary'];

// ─── Pagination ────────────────────────────────────────────────────────
export type PageOfTraceSummary = Schemas['PageOfTraceSummary'];
export type PageOfLogEntry = Schemas['PageOfLogEntry'];

// ─── Request shapes ────────────────────────────────────────────────────

/** A closed time window in UTC unix milliseconds — the wire unit everywhere. */
export interface TimeRange {
  startUnixMs: number;
  endUnixMs: number;
}

export interface ListTracesRequest extends TimeRange {
  service?: string;
  operation?: string;
  minDurationMs?: number;
  maxDurationMs?: number;
  cursor?: string;
  limit?: number;
}

export interface ListLogsRequest extends Partial<TimeRange> {
  traceId?: TraceId;
  /** Log stream / service filter, as the backend's `Stream` parameter. */
  stream?: string;
  limit?: number;
}

export interface ListErrorsRequest extends TimeRange {
  service?: string;
  limit?: number;
}
