import { mockApi } from './mock-data';
import type {
  HealthResponse,
  ListLogsRequest,
  ListLogsResponse,
  ListServicesRequest,
  ListServicesResponse,
  ListTracesRequest,
  ListTracesResponse,
  TraceDetail,
} from './types';

/**
 * Tessera API client. Falls back to mock data in dev when VITE_API_BASE_URL
 * is unset, so the app works without a running backend.
 *
 * Usage:
 *   import { api } from '@/shared/api/client';
 *   const traces = await api.listTraces({ startUnixMs, endUnixMs });
 */

const API_BASE = import.meta.env['VITE_API_BASE_URL'] ?? '';
const USE_MOCK = !API_BASE;

class ApiError extends Error {
  constructor(public readonly status: number, message: string) {
    super(message);
    this.name = 'ApiError';
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`${API_BASE}${path}`, {
    ...init,
    headers: { 'Content-Type': 'application/json', ...init?.headers },
    credentials: 'include',
  });
  if (!res.ok) {
    const body = await res.text().catch(() => '');
    throw new ApiError(res.status, `${res.status} ${res.statusText}: ${body}`);
  }
  return res.json() as Promise<T>;
}

export const api = {
  /** List traces in time range. */
  listTraces(req: ListTracesRequest): Promise<ListTracesResponse> {
    if (USE_MOCK) return mockApi.listTraces(req);
    const params = new URLSearchParams({
      start: String(req.startUnixMs),
      end: String(req.endUnixMs),
      ...(req.service && { service: req.service }),
      ...(req.operation && { operation: req.operation }),
      ...(req.minDurationMs && { minDurationMs: String(req.minDurationMs) }),
      ...(req.maxDurationMs && { maxDurationMs: String(req.maxDurationMs) }),
      ...(req.limit && { limit: String(req.limit) }),
    });
    return request<ListTracesResponse>(`/api/traces?${params.toString()}`);
  },

  /** Get full trace detail with span tree. */
  getTrace(traceId: string): Promise<TraceDetail> {
    if (USE_MOCK) {
      // Mock: synthesize a tiny trace from summary if present
      return import('./mock-data').then(({ mockData }) => {
        const summary = mockData.traces.find((t) => t.traceId === traceId);
        if (!summary) throw new ApiError(404, `Trace ${traceId} not found`);
        return {
          traceId: summary.traceId,
          rootService: summary.rootService,
          rootOperation: summary.rootOperation,
          startTime: summary.startTime,
          durationMs: summary.durationMs,
          status: summary.status,
          spans: [
            {
              spanId: 'root',
              parentSpanId: null,
              service: summary.rootService,
              operation: summary.rootOperation,
              startTime: summary.startTime,
              durationMs: summary.durationMs,
              status: summary.status,
              tags: { 'http.method': 'POST', 'http.status_code': summary.status === 'error' ? '500' : '200' },
              events: [],
            },
          ],
        };
      });
    }
    return request<TraceDetail>(`/api/traces/${encodeURIComponent(traceId)}`);
  },

  /** List logs (optionally filtered by trace_id or LogsQL query). */
  listLogs(req: ListLogsRequest): Promise<ListLogsResponse> {
    if (USE_MOCK) return mockApi.listLogs(req);
    const params = new URLSearchParams();
    if (req.traceId) params.set('trace_id', req.traceId);
    if (req.query) params.set('q', req.query);
    if (req.startUnixMs) params.set('start', String(req.startUnixMs));
    if (req.endUnixMs) params.set('end', String(req.endUnixMs));
    if (req.limit) params.set('limit', String(req.limit));
    return request<ListLogsResponse>(`/api/logs?${params.toString()}`);
  },

  /** List services with span counts. */
  listServices(req: ListServicesRequest): Promise<ListServicesResponse> {
    if (USE_MOCK) return mockApi.listServices();
    const params = new URLSearchParams({
      start: String(req.startUnixMs),
      end: String(req.endUnixMs),
    });
    return request<ListServicesResponse>(`/api/services?${params.toString()}`);
  },

  /** Health check. */
  getHealth(): Promise<HealthResponse> {
    if (USE_MOCK) return mockApi.getHealth();
    return request<HealthResponse>('/api/health');
  },
};

export { ApiError, USE_MOCK };