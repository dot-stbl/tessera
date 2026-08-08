import type { TesseraApi } from './client';
import { buildFleet } from './mock-fleet';
import { TesseraApiError } from './problem-details';
import type {
  DependencyGraph,
  ErrorGroupSummary,
  GetTraceResponse,
  HealthResponse,
  LogEntry,
  LogLevel,
  LogMarker,
  Resource,
  ServiceRedResponse,
  ServiceSummary,
  Span,
  SpanKind,
  TraceStatus,
  TraceSummary,
} from './types';

/**
 * Mock implementation of {@link TesseraApi} for developing without a backend.
 *
 * It is a peer of the HTTP client, not a branch inside it, so the two cannot
 * disagree about shapes — the compiler checks this file against the generated
 * schema exactly as it checks the real one.
 *
 * The fixtures are deliberately not minimal. The previous mock synthesized a
 * single root span per trace, which made the waterfall — the screen this app
 * exists for — impossible to build against mock data. The flagship trace here
 * has a 23-span tree four levels deep, an error path, logs that carry
 * `spanId`, and timeline markers, so every part of the request view has
 * something real to render.
 */

const MINUTE = 60_000;

/** Fixed clock so fixtures are stable within a session. */
const NOW = Date.now();

const RESOURCES: Record<string, Resource> = {
  'checkout-api': res('checkout-api', 'shop'),
  postgres: res('postgres', 'data'),
  stripe: res('stripe', 'payments'),
  pricing: res('pricing', 'shop'),
  'frontend-proxy': res('frontend-proxy', 'edge'),
  currency: res('currency', 'shop'),
};

function res(serviceName: string, namespace: string): Resource {
  return {
    serviceName,
    serviceNamespace: namespace,
    deploymentEnvironment: 'staging',
    attributes: { 'host.name': `${serviceName}-7f4c9`, 'telemetry.sdk.language': 'dotnet' },
  };
}

interface SpanSeed {
  id: string;
  parent: string | null;
  service: keyof typeof RESOURCES;
  operation: string;
  /** Offset from trace start, ms. */
  at: number;
  durationMs: number;
  kind: SpanKind;
  status?: TraceStatus;
  tags?: Record<string, string>;
}

const FLAGSHIP_TRACE_ID = 'a1b2c3d4e5f67890abcdef0123456789';
const CHARGE_SPAN_ID = '7f3a91c4b2e50d68';
const TRACE_START = NOW - 5 * MINUTE;

/**
 * The checkout outage. A Stripe call times out 8 s into a retry budget, the
 * order is rolled back, and the root span fails — the story every other screen
 * in the app is navigating towards.
 */
const FLAGSHIP_SPANS: SpanSeed[] = [
  { id: '0a1b2c3d4e5f6071', parent: null, service: 'frontend-proxy', operation: 'ingress POST /checkout', at: 0, durationMs: 1247, kind: 'server', status: 'error', tags: { 'http.request.method': 'POST', 'url.path': '/checkout', 'http.response.status_code': '500' } },
  { id: '1a1b2c3d4e5f6072', parent: '0a1b2c3d4e5f6071', service: 'checkout-api', operation: 'POST /checkout', at: 6, durationMs: 1235, kind: 'server', status: 'error', tags: { 'http.request.method': 'POST', 'http.route': '/checkout', 'http.response.status_code': '500', 'enduser.id': 'u_8842' } },
  { id: '2a1b2c3d4e5f6073', parent: '1a1b2c3d4e5f6072', service: 'checkout-api', operation: 'validate cart', at: 12, durationMs: 34, kind: 'internal' },
  { id: '3a1b2c3d4e5f6074', parent: '2a1b2c3d4e5f6073', service: 'postgres', operation: 'SELECT cart', at: 18, durationMs: 24, kind: 'client', tags: { 'db.system.name': 'postgresql', 'db.collection.name': 'cart' } },
  { id: '4a1b2c3d4e5f6075', parent: '1a1b2c3d4e5f6072', service: 'postgres', operation: 'SELECT cart_items', at: 52, durationMs: 88, kind: 'client', tags: { 'db.system.name': 'postgresql', 'db.collection.name': 'cart_items' } },
  { id: '5a1b2c3d4e5f6076', parent: '1a1b2c3d4e5f6072', service: 'pricing', operation: 'GET /quote', at: 146, durationMs: 178, kind: 'client', tags: { 'server.address': 'pricing.internal', 'http.response.status_code': '200' } },
  { id: '6a1b2c3d4e5f6077', parent: '5a1b2c3d4e5f6076', service: 'pricing', operation: 'resolve tax band', at: 152, durationMs: 41, kind: 'internal' },
  { id: '7a1b2c3d4e5f6078', parent: '5a1b2c3d4e5f6076', service: 'currency', operation: 'Convert EUR→USD', at: 198, durationMs: 22, kind: 'client', tags: { 'rpc.service': 'currency.Converter' } },
  { id: '8a1b2c3d4e5f6079', parent: '5a1b2c3d4e5f6076', service: 'pricing', operation: 'cache write', at: 288, durationMs: 9, kind: 'internal' },
  { id: '9a1b2c3d4e5f607a', parent: '1a1b2c3d4e5f6072', service: 'checkout-api', operation: 'reserve inventory', at: 330, durationMs: 62, kind: 'internal' },
  { id: 'aa1b2c3d4e5f607b', parent: '9a1b2c3d4e5f607a', service: 'postgres', operation: 'UPDATE inventory', at: 338, durationMs: 47, kind: 'client', tags: { 'db.system.name': 'postgresql', 'db.operation.name': 'UPDATE' } },
  { id: CHARGE_SPAN_ID, parent: '1a1b2c3d4e5f6072', service: 'stripe', operation: 'POST /v1/charges', at: 350, durationMs: 862, kind: 'client', status: 'error', tags: { 'server.address': 'api.stripe.com', 'server.port': '443', 'http.request.method': 'POST', 'http.response.status_code': '504', 'peer.service': 'stripe' } },
  { id: 'ba1b2c3d4e5f607c', parent: CHARGE_SPAN_ID, service: 'stripe', operation: 'tls handshake', at: 356, durationMs: 136, kind: 'internal', tags: { 'tls.protocol.version': '1.3' } },
  { id: 'ca1b2c3d4e5f607d', parent: CHARGE_SPAN_ID, service: 'stripe', operation: 'http.send attempt 1', at: 496, durationMs: 240, kind: 'internal', status: 'error', tags: { 'http.request.resend_count': '0' } },
  { id: 'da1b2c3d4e5f607e', parent: CHARGE_SPAN_ID, service: 'stripe', operation: 'http.send attempt 2', at: 742, durationMs: 240, kind: 'internal', status: 'error', tags: { 'http.request.resend_count': '1' } },
  { id: 'ea1b2c3d4e5f607f', parent: CHARGE_SPAN_ID, service: 'stripe', operation: 'http.send attempt 3', at: 988, durationMs: 222, kind: 'internal', status: 'error', tags: { 'http.request.resend_count': '2' } },
  { id: 'fa1b2c3d4e5f6080', parent: '1a1b2c3d4e5f6072', service: 'checkout-api', operation: 'compensate order', at: 1216, durationMs: 19, kind: 'internal' },
  { id: '0b1b2c3d4e5f6081', parent: 'fa1b2c3d4e5f6080', service: 'postgres', operation: 'UPDATE orders', at: 1220, durationMs: 11, kind: 'client', tags: { 'db.system.name': 'postgresql', 'db.operation.name': 'UPDATE' } },
  { id: '1b1b2c3d4e5f6082', parent: 'fa1b2c3d4e5f6080', service: 'postgres', operation: 'DELETE inventory_hold', at: 1228, durationMs: 6, kind: 'client', tags: { 'db.system.name': 'postgresql' } },
  { id: '2b1b2c3d4e5f6083', parent: '1a1b2c3d4e5f6072', service: 'checkout-api', operation: 'publish order.failed', at: 1234, durationMs: 4, kind: 'producer', tags: { 'messaging.system': 'rabbitmq', 'messaging.destination.name': 'orders' } },
  { id: '3b1b2c3d4e5f6084', parent: '0a1b2c3d4e5f6071', service: 'frontend-proxy', operation: 'render error page', at: 1241, durationMs: 5, kind: 'internal' },
  { id: '4b1b2c3d4e5f6085', parent: '2a1b2c3d4e5f6073', service: 'checkout-api', operation: 'coupon lookup', at: 30, durationMs: 8, kind: 'internal' },
  { id: '5b1b2c3d4e5f6086', parent: '9a1b2c3d4e5f607a', service: 'checkout-api', operation: 'stock policy check', at: 386, durationMs: 5, kind: 'internal' },
];

function toSpan(seed: SpanSeed): Span {
  return {
    spanId: seed.id,
    parentSpanId: seed.parent,
    service: seed.service,
    operation: seed.operation,
    startTime: TRACE_START + seed.at,
    durationMs: seed.durationMs,
    status: seed.status ?? 'ok',
    kind: seed.kind,
    resource: RESOURCES[seed.service] as Resource,
    tags: seed.tags ?? {},
    events:
      seed.status === 'error' && seed.service === 'stripe' && seed.id === CHARGE_SPAN_ID
        ? [
            {
              time: TRACE_START + seed.at + seed.durationMs - 4,
              name: 'exception',
              attributes: {
                'exception.type': 'TimeoutRejectedException',
                'exception.message': 'HTTP request timed out after 8000ms',
                'exception.stacktrace':
                  'at Polly.Timeout.TimeoutStrategy.ExecuteCore\n   at Tessera.Sample.PaymentClient.ChargeAsync',
              },
            },
          ]
        : [],
  };
}

const FLAGSHIP_TRACE_SPANS: Span[] = FLAGSHIP_SPANS.map(toSpan);

function log(
  offsetMs: number,
  level: LogLevel,
  service: string,
  message: string,
  spanId: string | null,
  fields: Record<string, string> = {},
): LogEntry {
  return {
    timestamp: TRACE_START + offsetMs,
    level,
    service,
    traceId: FLAGSHIP_TRACE_ID,
    spanId,
    message,
    fields,
  };
}

/**
 * Logs for the flagship trace. Several carry {@link CHARGE_SPAN_ID} so that
 * selecting the failing Stripe span narrows the panel to a real subset — the
 * behaviour the product is built around — while others sit on sibling spans or
 * carry no span id at all, which is the case the UI must not silently merge in.
 */
const FLAGSHIP_LOGS: LogEntry[] = [
  log(8, 'info', 'checkout-api', 'checkout requested user=u_8842 items=3 total=149.97 EUR', '1a1b2c3d4e5f6072', { 'enduser.id': 'u_8842', 'cart.items': '3' }),
  log(44, 'debug', 'postgres', 'SELECT cart WHERE user_id = $1 (24ms)', '3a1b2c3d4e5f6074', { 'db.rows': '1' }),
  log(150, 'info', 'pricing', 'quote resolved from cache ttl=42s', '5a1b2c3d4e5f6076', { 'cache.hit': 'true' }),
  log(352, 'info', 'stripe', 'POST /v1/charges amount=14997 currency=eur idempotency_key=ik_9f2c', CHARGE_SPAN_ID, { 'http.request.method': 'POST', 'idempotency_key': 'ik_9f2c' }),
  log(740, 'warn', 'stripe', 'retry 1/3 after connect timeout (8000ms budget)', CHARGE_SPAN_ID, { 'retry.attempt': '1', 'retry.max': '3' }),
  log(986, 'warn', 'stripe', 'retry 2/3 after connect timeout (8000ms budget)', CHARGE_SPAN_ID, { 'retry.attempt': '2', 'retry.max': '3' }),
  log(1208, 'error', 'stripe', 'gateway timeout · no response within 8000ms', CHARGE_SPAN_ID, { 'error.type': 'TimeoutRejectedException' }),
  log(1218, 'error', 'checkout-api', 'order 8842 rolled back · payment stage failed', 'fa1b2c3d4e5f6080', { 'order.id': '8842' }),
  log(1236, 'info', 'checkout-api', 'published order.failed to orders exchange', '2b1b2c3d4e5f6083', {}),
  log(1244, 'error', 'frontend-proxy', 'POST /checkout 500 1247ms upstream=checkout-api', '0a1b2c3d4e5f6071', { 'http.response.status_code': '500' }),
  log(1246, 'info', 'checkout-api', 'request accounting flushed', null, { note: 'emitted outside any span' }),
];

const FLAGSHIP_MARKERS: LogMarker[] = FLAGSHIP_LOGS.filter(
  (entry) => entry.level === 'warn' || entry.level === 'error',
).map((entry) => ({
  spanId: entry.spanId,
  offsetMs: entry.timestamp - TRACE_START,
  level: entry.level,
  message: entry.message,
  timestampUnixMs: entry.timestamp,
}));

function summary(
  traceId: string,
  rootService: string,
  rootOperation: string,
  minutesAgo: number,
  durationMs: number,
  status: TraceStatus,
  spanCount: number,
  services: string[],
): TraceSummary {
  return {
    traceId,
    rootService,
    rootOperation,
    startTime: NOW - minutesAgo * MINUTE,
    durationMs,
    status,
    spanCount,
    services,
  };
}

/**
 * The generated day of traffic behind the flagship. See mock-fleet.ts — 220
 * traces across 24 hours, each with a real span tree and its own logs, so the
 * inventory, the RED figures and the log stream all agree with what a trace
 * shows when it is opened.
 */
const FLEET = buildFleet(NOW);

const TRACES: TraceSummary[] = [
  summary(FLAGSHIP_TRACE_ID, 'frontend-proxy', 'ingress POST /checkout', 5, 1247, 'error', FLAGSHIP_SPANS.length, ['frontend-proxy', 'checkout-api', 'postgres', 'pricing', 'stripe', 'currency']),
  ...FLEET.traces,
].sort((a, b) => b.startTime - a.startTime);

const SERVICES: ServiceSummary[] = FLEET.services;

/**
 * Error groups, rolled up from what the fleet actually failed at. Hand-writing
 * these let them drift from the traces they claim to sample, which is the one
 * thing an error-grouping screen must never do.
 */
const ERROR_GROUPS: ErrorGroupSummary[] = (() => {
  const groups = new Map<string, ErrorGroupSummary>();

  for (const trace of FLEET.traces) {
    if (trace.status !== 'error') continue;
    const detail = FLEET.detail(trace.traceId);
    for (const span of detail?.spans ?? []) {
      const type = span.tags['error.type'];
      if (type === undefined) continue;

      const key = `${span.service}|${type}`;
      const existing = groups.get(key);
      if (existing) {
        existing.count = (existing.count ?? 0) + 1;
        if (existing.sampleTraceIds.length < 5) existing.sampleTraceIds.push(trace.traceId);
      } else {
        groups.set(key, {
          key,
          exceptionType: type,
          message: span.events[0]?.attributes['exception.message'] ?? `${span.operation} failed`,
          count: 1,
          sampleTraceIds: [trace.traceId],
        });
      }
    }
  }

  return [
    {
      key: 'stripe|TimeoutRejectedException',
      exceptionType: 'TimeoutRejectedException',
      message: 'HTTP request timed out after 8000ms',
      count: 96,
      sampleTraceIds: [FLAGSHIP_TRACE_ID],
    },
    ...[...groups.values()].sort((a, b) => (b.count ?? 0) - (a.count ?? 0)),
  ];
})();

/** Flagship per-trace dependency mini-map (CLIENT edges only). */
const FLAGSHIP_DEPS: DependencyGraph = {
  nodes: [
    { id: 'frontend-proxy', name: 'frontend-proxy', kind: 'service' },
    { id: 'checkout-api', name: 'checkout-api', kind: 'service' },
    { id: 'pricing', name: 'pricing', kind: 'service' },
    { id: 'currency', name: 'currency', kind: 'service' },
    { id: 'db:postgresql:cart', name: 'cart', kind: 'database' },
    { id: 'db:postgresql:cart_items', name: 'cart_items', kind: 'database' },
    { id: 'db:postgresql', name: 'postgresql', kind: 'database' },
    { id: 'external:stripe', name: 'stripe', kind: 'external' },
  ],
  edges: [
    { fromId: 'frontend-proxy', toId: 'checkout-api', callCount: 1, errorCount: 0 },
    { fromId: 'checkout-api', toId: 'db:postgresql:cart', callCount: 1, errorCount: 0 },
    { fromId: 'checkout-api', toId: 'db:postgresql:cart_items', callCount: 1, errorCount: 0 },
    { fromId: 'checkout-api', toId: 'pricing', callCount: 1, errorCount: 0 },
    { fromId: 'pricing', toId: 'currency', callCount: 1, errorCount: 0 },
    { fromId: 'checkout-api', toId: 'db:postgresql', callCount: 3, errorCount: 0 },
    { fromId: 'checkout-api', toId: 'external:stripe', callCount: 1, errorCount: 1 },
  ],
};

const SERVICE_RED: Record<string, ServiceRedResponse> = FLEET.red;

const HEALTH: HealthResponse = {
  provider: 'victoria',
  status: 'healthy',
  detail: 'traces=healthy; logs=healthy; metrics=healthy (mock)',
};

/** Simulated latency so loading states are visible during development. */
function delay<T>(value: T, ms = 120): Promise<T> {
  return new Promise((resolve) => setTimeout(() => resolve(value), ms));
}

export function createMockApi(): TesseraApi {
  return {
    getHealth() {
      return delay(HEALTH, 60);
    },

    listServices() {
      return delay(SERVICES);
    },

    getServiceRed(serviceName) {
      const known = SERVICE_RED[serviceName];
      if (known) {
        return delay(known);
      }

      return delay<ServiceRedResponse>({
        requestRatePerSec: null,
        errorRatio: null,
        durationP95Ms: null,
        source: 'spanApprox',
      });
    },

    listTraces(request) {
      const items = TRACES.filter((trace) => {
        if (request.service && !trace.services.includes(request.service)) return false;
        if (request.minDurationMs !== undefined && trace.durationMs < request.minDurationMs) return false;
        if (request.maxDurationMs !== undefined && trace.durationMs > request.maxDurationMs) return false;
        if (request.operation && !trace.rootOperation.toLowerCase().includes(request.operation.toLowerCase())) return false;
        return trace.startTime >= request.startUnixMs && trace.startTime <= request.endUnixMs;
      });

      const limit = request.limit ?? 50;
      return delay({
        items: items.slice(0, limit),
        cursor: null,
        hasMore: items.length > limit,
      });
    },

    getTrace(traceId) {
      if (traceId === FLAGSHIP_TRACE_ID) {
        const trace = TRACES[0] as TraceSummary;
        const response: GetTraceResponse = {
          trace: {
            traceId: FLAGSHIP_TRACE_ID,
            rootService: trace.rootService,
            rootOperation: trace.rootOperation,
            startTime: TRACE_START,
            durationMs: trace.durationMs,
            status: trace.status,
            spans: FLAGSHIP_TRACE_SPANS,
          },
          correlatedLogs: FLAGSHIP_LOGS,
          mode: 'full',
          markers: FLAGSHIP_MARKERS,
          erroredSpanCount: FLAGSHIP_TRACE_SPANS.filter((span) => span.status === 'error').length,
          exceptions: [
            {
              spanId: CHARGE_SPAN_ID,
              service: 'stripe',
              operation: 'POST /v1/charges',
              exceptionType: 'TimeoutRejectedException',
              exceptionMessage: 'HTTP request timed out after 8000ms',
            },
          ],
          dependencyGraph: FLAGSHIP_DEPS,
        };
        return delay(response, 180);
      }

      const known = TRACES.find((candidate) => candidate.traceId === traceId);
      const built = FLEET.detail(traceId);
      if (!known || !built) {
        return Promise.reject(
          new TesseraApiError(404, `Trace ${traceId} not found`, {
            title: 'Not Found',
            status: 404,
            detail: `Trace ${traceId} not found`,
          }),
        );
      }

      // Every fleet trace opens into a real waterfall with its own logs. The
      // previous fixtures synthesized a single root span here, so nine of ten
      // rows in the list led to a screen that could not demonstrate the product.
      return delay<GetTraceResponse>({
        trace: {
          traceId: known.traceId,
          rootService: known.rootService,
          rootOperation: known.rootOperation,
          startTime: known.startTime,
          durationMs: known.durationMs,
          status: known.status,
          spans: built.spans,
        },
        correlatedLogs: built.logs,
        mode: built.logs.length > 0 ? 'full' : 'spansOnly',
        markers: built.markers,
        erroredSpanCount: built.spans.filter((span) => span.status === 'error').length,
        exceptions: built.spans
          .filter((span) => span.status === 'error')
          .map((span) => ({
            spanId: span.spanId,
            service: span.service,
            operation: span.operation,
            exceptionType: span.tags['error.type'] ?? 'Exception',
            exceptionMessage: span.events[0]?.attributes['exception.message'] ?? 'failed',
          })),
        dependencyGraph: built.dependencyGraph,
      });
    },

    listLogs(request) {
      let items = [...FLAGSHIP_LOGS, ...FLEET.logStream].sort(
        (a, b) => a.timestamp - b.timestamp,
      );
      if (request.traceId) {
        items = items.filter((entry) => entry.traceId === request.traceId);
      }
      if (request.stream) {
        items = items.filter((entry) => entry.service === request.stream);
      }
      const from = request.startUnixMs ?? 0;
      const to = request.endUnixMs ?? Number.MAX_SAFE_INTEGER;
      items = items.filter((entry) => entry.timestamp >= from && entry.timestamp <= to);

      // Newest last, and the window's tail is what a stream shows — taking the
      // head would pin the view to the oldest entries in the range and never
      // move as time passes.
      const limit = request.limit ?? 500;
      return delay({
        items: items.slice(-limit),
        cursor: null,
        hasMore: items.length > limit,
      });
    },

    listErrors(request) {
      const limit = request.limit ?? 50;
      return delay(ERROR_GROUPS.slice(0, limit));
    },
  };
}

/** Ids the mock knows about, for stories and tests that need a real target. */
export const mockIds = {
  flagshipTraceId: FLAGSHIP_TRACE_ID,
  chargeSpanId: CHARGE_SPAN_ID,
} as const;
