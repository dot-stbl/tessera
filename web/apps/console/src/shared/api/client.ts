import { apiRoutes } from './routes';
import { toApiError } from './problem-details';
import type {
  ErrorGroupSummary,
  GetTraceResponse,
  HealthResponse,
  ListErrorsRequest,
  ListLogsRequest,
  ListTracesRequest,
  PageOfLogEntry,
  PageOfTraceSummary,
  ServiceSummary,
  TimeRange,
} from './types';

/**
 * The API surface, as an interface rather than a concrete object.
 *
 * Having a named contract is what lets the mock be a *peer implementation*
 * instead of a branch inside every method. The previous client tested
 * `if (USE_MOCK)` in each function, which shipped the fixtures in the production
 * bundle and — more damagingly — let the two paths drift silently: the mock
 * `getTrace` returned a single root span, so the waterfall could not be
 * developed against it at all.
 *
 * Every method takes an `AbortSignal` because TanStack Query hands one to each
 * `queryFn`; without it a filter change leaves the superseded request running.
 */
export interface TesseraApi {
  getHealth(signal?: AbortSignal): Promise<HealthResponse>;
  listServices(range: TimeRange, signal?: AbortSignal): Promise<ServiceSummary[]>;
  listTraces(request: ListTracesRequest, signal?: AbortSignal): Promise<PageOfTraceSummary>;
  getTrace(traceId: string, signal?: AbortSignal): Promise<GetTraceResponse>;
  listLogs(request: ListLogsRequest, signal?: AbortSignal): Promise<PageOfLogEntry>;
  listErrors(request: ListErrorsRequest, signal?: AbortSignal): Promise<ErrorGroupSummary[]>;
}

/**
 * Query-parameter names are the PascalCase spellings the OpenAPI document
 * declares, which come from the C# request-model property names. ASP.NET's query
 * binding happens to be case-insensitive, but matching the document is what
 * keeps this client and a generated one interchangeable.
 */
function query(params: Record<string, string | number | undefined>): string {
  const search = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    // Explicit undefined check, not falsiness: `MinDurationMs=0` and `Limit=0`
    // are meaningful values that a truthiness test silently drops.
    if (value !== undefined) {
      search.set(key, String(value));
    }
  }
  const rendered = search.toString();
  return rendered ? `?${rendered}` : '';
}

/** Talks to a real Tessera host. */
export function createHttpApi(baseUrl: string): TesseraApi {
  async function request<T>(path: string, signal?: AbortSignal): Promise<T> {
    const response = await fetch(`${baseUrl}${path}`, {
      method: 'GET',
      headers: { Accept: 'application/json' },
      signal,
    });

    if (!response.ok) {
      throw await toApiError(response);
    }

    return (await response.json()) as T;
  }

  return {
    getHealth(signal) {
      return request<HealthResponse>(apiRoutes.health, signal);
    },

    listServices(range, signal) {
      return request<ServiceSummary[]>(
        apiRoutes.services + query({ StartUnixMs: range.startUnixMs, EndUnixMs: range.endUnixMs }),
        signal,
      );
    },

    listTraces(request_, signal) {
      return request<PageOfTraceSummary>(
        apiRoutes.traces +
          query({
            Service: request_.service,
            Operation: request_.operation,
            StartUnixMs: request_.startUnixMs,
            EndUnixMs: request_.endUnixMs,
            MinDurationMs: request_.minDurationMs,
            MaxDurationMs: request_.maxDurationMs,
            Cursor: request_.cursor,
            Limit: request_.limit,
          }),
        signal,
      );
    },

    getTrace(traceId, signal) {
      return request<GetTraceResponse>(apiRoutes.trace(traceId), signal);
    },

    listLogs(request_, signal) {
      return request<PageOfLogEntry>(
        apiRoutes.logs +
          query({
            TraceId: request_.traceId,
            Stream: request_.stream,
            StartUnixMs: request_.startUnixMs,
            EndUnixMs: request_.endUnixMs,
            Limit: request_.limit,
          }),
        signal,
      );
    },

    listErrors(request_, signal) {
      return request<ErrorGroupSummary[]>(
        apiRoutes.errors +
          query({
            StartUnixMs: request_.startUnixMs,
            EndUnixMs: request_.endUnixMs,
            Service: request_.service,
            Limit: request_.limit,
          }),
        signal,
      );
    },
  };
}
