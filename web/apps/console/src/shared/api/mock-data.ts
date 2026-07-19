import type {
  HealthResponse,
  ListLogsResponse,
  ListServicesResponse,
  LogEntry,
  ServiceSummary,
  TraceSummary,
} from './types';

/**
 * Mock data for development without a real backend.
 * Activated when VITE_API_BASE_URL is empty / unset.
 */

const now = Date.now();
const HOUR = 60 * 60 * 1000;
const MIN = 60 * 1000;

const SERVICES = ['checkout-api', 'postgres', 'stripe', 'frontend-proxy', 'currency'];

function makeTrace(
  id: string,
  service: string,
  operation: string,
  minutesAgo: number,
  durationMs: number,
  status: 'ok' | 'error' = 'ok',
  spanCount = 8,
): TraceSummary {
  return {
    traceId: id,
    rootService: service,
    rootOperation: operation,
    startTime: now - minutesAgo * MIN,
    durationMs,
    status,
    spanCount,
    services: [service, ...SERVICES.slice(0, 2).filter((s) => s !== service)],
  };
}

const MOCK_TRACES: TraceSummary[] = [
  makeTrace('a1b2c3d4e5f67890abcdef0123456789', 'checkout-api', 'POST /checkout', 5, 1247, 'error', 23),
  makeTrace('b2c3d4e5f67890abcdef0123456789ab', 'checkout-api', 'GET /cart', 12, 89, 'ok', 5),
  makeTrace('c3d4e5f67890abcdef0123456789abcd', 'frontend-proxy', 'ingress', 18, 412, 'ok', 12),
  makeTrace('d4e5f67890abcdef0123456789abcdef', 'currency', 'Convert', 25, 23, 'ok', 4),
  makeTrace('e5f67890abcdef0123456789abcdef01', 'postgres', 'SELECT orders', 32, 156, 'ok', 7),
  makeTrace('f67890abcdef0123456789abcdef012', 'stripe', 'Charge', 47, 8934, 'error', 19),
  makeTrace('67890abcdef0123456789abcdef0123', 'checkout-api', 'POST /payment', 58, 234, 'ok', 11),
  makeTrace('7890abcdef0123456789abcdef01234', 'frontend-proxy', 'ingress', 78, 287, 'ok', 9),
  makeTrace('890abcdef0123456789abcdef012345', 'currency', 'Convert', 92, 19, 'ok', 3),
  makeTrace('90abcdef0123456789abcdef0123456', 'checkout-api', 'GET /cart', 105, 67, 'ok', 4),
];

const MOCK_LOGS: LogEntry[] = [
  {
    timestamp: now - 5 * MIN,
    level: 'error',
    service: 'checkout-api',
    traceId: 'a1b2c3d4e5f67890abcdef0123456789',
    spanId: 'span_001',
    message: 'failed to charge card: StripeCardError',
    fields: { 'error.kind': 'StripeCardError', 'user.id': 'u_42', 'amount': '129.99' },
  },
  {
    timestamp: now - 5 * MIN + 100,
    level: 'warn',
    service: 'checkout-api',
    traceId: 'a1b2c3d4e5f67890abcdef0123456789',
    spanId: 'span_001',
    message: 'payment retry attempt 1 of 3',
    fields: { 'retry.attempt': '1', 'retry.max': '3' },
  },
  {
    timestamp: now - 12 * MIN,
    level: 'info',
    service: 'checkout-api',
    traceId: 'b2c3d4e5f67890abcdef0123456789ab',
    message: 'cart fetched for user',
    fields: { 'user.id': 'u_42', 'cart.size': '3' },
  },
  {
    timestamp: now - 18 * MIN,
    level: 'info',
    service: 'frontend-proxy',
    traceId: 'c3d4e5f67890abcdef0123456789abcd',
    message: 'request routed to backend',
  },
  {
    timestamp: now - 32 * MIN,
    level: 'debug',
    service: 'postgres',
    traceId: 'e5f67890abcdef0123456789abcdef01',
    message: 'SELECT orders WHERE user_id = $1',
  },
  {
    timestamp: now - 47 * MIN,
    level: 'error',
    service: 'stripe',
    traceId: 'f67890abcdef0123456789abcdef012',
    message: 'card declined by issuer',
    fields: { 'decline.code': 'insufficient_funds' },
  },
  {
    timestamp: now - HOUR,
    level: 'fatal',
    service: 'stripe',
    traceId: 'f67890abcdef0123456789abcdef012',
    message: 'payment processor unreachable',
  },
  {
    timestamp: now - HOUR - 5 * MIN,
    level: 'info',
    service: 'checkout-api',
    message: 'service started',
  },
];

const MOCK_SERVICES: ServiceSummary[] = SERVICES.map((name, i) => ({
  name,
  spanCount: [8421, 12000, 4521, 18392, 3210][i] ?? 1000,
  errorCount: [23, 0, 2, 5, 1][i] ?? 0,
  operations: [
    { name: ['POST /checkout', 'GET /cart', 'POST /payment'][i] ?? `${name}.op1`, count: 4210 },
    { name: ['POST /checkout', 'SELECT orders', 'GET /products'][i] ?? `${name}.op2`, count: 2100 },
  ],
}));

const MOCK_HEALTH: HealthResponse = {
  status: 'healthy',
  backends: {
    vt: { status: 'ok', latencyMs: 23 },
    vl: { status: 'ok', latencyMs: 18 },
    vm: { status: 'ok', latencyMs: 31 },
  },
};

// Simulated network delay for realistic dev UX (50–200ms)
function delay<T>(value: T, ms = 100): Promise<T> {
  return new Promise((resolve) => setTimeout(() => resolve(value), ms));
}

export const mockApi = {
  listTraces: (params: { service?: string; limit?: number; startUnixMs: number; endUnixMs: number }) =>
    delay<{ items: TraceSummary[]; hasMore: boolean }>({
      items: MOCK_TRACES.filter((t) => !params.service || t.rootService === params.service).slice(
        0,
        params.limit ?? 50,
      ),
      hasMore: false,
    }),

  listLogs: (params: { traceId?: string; limit?: number }) =>
    delay<ListLogsResponse>({
      entries: MOCK_LOGS.filter((l) => !params.traceId || l.traceId === params.traceId).slice(
        0,
        params.limit ?? 500,
      ),
      total: MOCK_LOGS.length,
    }),

  listServices: () =>
    delay<ListServicesResponse>({
      items: MOCK_SERVICES,
    }),

  getHealth: () => delay<HealthResponse>(MOCK_HEALTH),
};

// Also export the data directly for components that want to render raw
export const mockData = {
  traces: MOCK_TRACES,
  logs: MOCK_LOGS,
  services: MOCK_SERVICES,
  health: MOCK_HEALTH,
};