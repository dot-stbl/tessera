import type {
  DependencyGraph,
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
 * The fleet: a generated day of traffic behind the hand-authored flagship trace.
 *
 * Ten fixtures were enough to prove the wire shapes and useless for everything
 * else — a trace list with seven rows cannot show what ranking, filtering or a
 * latency distribution look like, and a screen designed against it is designed
 * against a blank page. This generates ~200 traces across 24 hours from a
 * handful of realistic request shapes, each with a real span tree and its own
 * logs, so every screen has something to be dense about.
 *
 * Everything here is **deterministic**: a seeded PRNG keyed off the trace id.
 * The same id yields the same tree on every render, in every session and in
 * screenshots — `Math.random()` would make the waterfall reshuffle under the
 * cursor and make visual diffs meaningless.
 */

const MINUTE = 60_000;
const HOUR = 60 * MINUTE;

/** mulberry32 — small, fast, and good enough for fixtures. */
function rng(seed: number): () => number {
  let a = seed >>> 0;
  return () => {
    a = (a + 0x6d2b79f5) >>> 0;
    let t = a;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

function hash(value: string): number {
  let h = 2166136261;
  for (let i = 0; i < value.length; i += 1) {
    h ^= value.charCodeAt(i);
    h = Math.imul(h, 16777619);
  }
  return h >>> 0;
}

/** Pick from a list by a 0–1 draw. */
function pick<T>(items: readonly T[], draw: number): T {
  return items[Math.min(items.length - 1, Math.floor(draw * items.length))] as T;
}

/**
 * Log-normal-ish jitter around 1. Latency is not symmetric — most requests sit
 * near the median and a few are far slower — so a uniform multiplier would give
 * a distribution no APM ever sees, and the log-scaled latency track nothing to
 * separate.
 */
function jitter(draw: number, spread = 0.55): number {
  return Math.exp((draw - 0.5) * 2 * spread);
}

// ─── Services ──────────────────────────────────────────────────────────────

const NAMESPACES: Record<string, string> = {
  'frontend-proxy': 'edge',
  'cdn-edge': 'edge',
  'checkout-api': 'shop',
  'cart-api': 'shop',
  'catalog-api': 'shop',
  pricing: 'shop',
  currency: 'shop',
  'search-api': 'search',
  'auth-api': 'identity',
  'notification-api': 'messaging',
  'image-resizer': 'media',
  postgres: 'data',
  redis: 'data',
  elasticsearch: 'data',
  rabbitmq: 'messaging',
  stripe: 'payments',
  paypal: 'payments',
};

export const FLEET_SERVICE_NAMES = Object.keys(NAMESPACES);

export const FLEET_RESOURCES: Record<string, Resource> = Object.fromEntries(
  Object.entries(NAMESPACES).map(([name, namespace]) => [
    name,
    {
      serviceName: name,
      serviceNamespace: namespace,
      deploymentEnvironment: 'staging',
      attributes: {
        'host.name': `${name}-${(hash(name) % 0xffff).toString(16).padStart(4, '0')}`,
        'telemetry.sdk.language': name === 'postgres' || name === 'redis' ? 'none' : 'dotnet',
        'service.version': `1.${hash(name) % 40}.${hash(name + 'p') % 12}`,
      },
    },
  ]),
);

// ─── Request shapes ────────────────────────────────────────────────────────

interface Step {
  service: string;
  operation: string;
  kind: SpanKind;
  /** Share of the parent's duration this step occupies. */
  weight: number;
  /** Probability the step is present at all. Absent steps model optional work. */
  chance?: number;
  /** Relative likelihood this step is the one that fails, when the trace fails. */
  blame?: number;
  tags?: Record<string, string>;
  children?: Step[];
}

interface Scenario {
  root: string;
  operation: string;
  /** Median root duration, ms. */
  p50: number;
  /** Share of these requests that fail. */
  errorRate: number;
  /** Relative traffic share. */
  volume: number;
  children: Step[];
}

const db = (operation: string, weight: number, extra?: Partial<Step>): Step => ({
  service: 'postgres',
  operation,
  kind: 'client',
  weight,
  tags: { 'db.system.name': 'postgresql' },
  ...extra,
});

const cache = (operation: string, weight: number): Step => ({
  service: 'redis',
  operation,
  kind: 'client',
  weight,
  tags: { 'db.system.name': 'redis' },
});

const SCENARIOS: Scenario[] = [
  {
    root: 'frontend-proxy',
    operation: 'ingress POST /checkout',
    p50: 640,
    errorRate: 0.09,
    volume: 14,
    children: [
      {
        service: 'checkout-api',
        operation: 'POST /checkout',
        kind: 'server',
        weight: 0.97,
        tags: { 'http.route': '/checkout' },
        children: [
          { service: 'auth-api', operation: 'verify session', kind: 'client', weight: 0.08 },
          db('SELECT cart', 0.1),
          { service: 'pricing', operation: 'GET /quote', kind: 'client', weight: 0.2, blame: 1, children: [
            { service: 'currency', operation: 'Convert EUR→USD', kind: 'client', weight: 0.3 },
            cache('GET quote:*', 0.1),
          ] },
          { service: 'checkout-api', operation: 'reserve inventory', kind: 'internal', weight: 0.12, children: [
            db('UPDATE inventory', 0.6),
          ] },
          { service: 'stripe', operation: 'POST /v1/charges', kind: 'client', weight: 0.44, blame: 6, tags: { 'server.address': 'api.stripe.com', 'peer.service': 'stripe' }, children: [
            { service: 'stripe', operation: 'tls handshake', kind: 'internal', weight: 0.2 },
            { service: 'stripe', operation: 'http.send', kind: 'internal', weight: 0.6, blame: 3 },
          ] },
          { service: 'rabbitmq', operation: 'publish order.created', kind: 'producer', weight: 0.04, tags: { 'messaging.system': 'rabbitmq' } },
        ],
      },
    ],
  },
  {
    root: 'frontend-proxy',
    operation: 'ingress GET /products',
    p50: 240,
    errorRate: 0.012,
    volume: 30,
    children: [
      {
        service: 'catalog-api',
        operation: 'GET /products',
        kind: 'server',
        weight: 0.95,
        children: [
          cache('GET catalog:page', 0.12),
          db('SELECT products', 0.4, { blame: 2 }),
          { service: 'pricing', operation: 'GET /quote batch', kind: 'client', weight: 0.3, children: [
            cache('MGET price:*', 0.2),
          ] },
          { service: 'image-resizer', operation: 'resize thumbnails', kind: 'client', weight: 0.2, chance: 0.5 },
        ],
      },
    ],
  },
  {
    root: 'frontend-proxy',
    operation: 'ingress GET /search',
    p50: 310,
    errorRate: 0.03,
    volume: 18,
    children: [
      {
        service: 'search-api',
        operation: 'GET /search',
        kind: 'server',
        weight: 0.96,
        children: [
          { service: 'elasticsearch', operation: 'POST _search', kind: 'client', weight: 0.62, blame: 5, tags: { 'db.system.name': 'elasticsearch' } },
          { service: 'catalog-api', operation: 'GET /products/hydrate', kind: 'client', weight: 0.22, children: [db('SELECT products IN', 0.7)] },
          cache('SETEX search:*', 0.05),
        ],
      },
    ],
  },
  {
    root: 'frontend-proxy',
    operation: 'ingress POST /cart',
    p50: 120,
    errorRate: 0.008,
    volume: 24,
    children: [
      {
        service: 'cart-api',
        operation: 'POST /cart',
        kind: 'server',
        weight: 0.94,
        children: [
          { service: 'auth-api', operation: 'verify session', kind: 'client', weight: 0.14 },
          db('UPSERT cart_items', 0.45, { blame: 2 }),
          cache('DEL cart:*', 0.06),
        ],
      },
    ],
  },
  {
    root: 'frontend-proxy',
    operation: 'ingress POST /login',
    p50: 180,
    errorRate: 0.021,
    volume: 12,
    children: [
      {
        service: 'auth-api',
        operation: 'POST /login',
        kind: 'server',
        weight: 0.95,
        children: [
          db('SELECT users', 0.25),
          { service: 'auth-api', operation: 'argon2 verify', kind: 'internal', weight: 0.5 },
          cache('SETEX session:*', 0.08),
          { service: 'notification-api', operation: 'send login alert', kind: 'client', weight: 0.12, chance: 0.25, blame: 2 },
        ],
      },
    ],
  },
  {
    root: 'checkout-api',
    operation: 'POST /payment/refund',
    p50: 890,
    errorRate: 0.14,
    volume: 5,
    children: [
      db('SELECT orders', 0.1),
      { service: 'stripe', operation: 'POST /v1/refunds', kind: 'client', weight: 0.5, blame: 5, tags: { 'server.address': 'api.stripe.com' } },
      { service: 'paypal', operation: 'POST /v2/payments/refund', kind: 'client', weight: 0.4, chance: 0.35, blame: 4 },
      db('UPDATE orders', 0.08),
      { service: 'rabbitmq', operation: 'publish refund.issued', kind: 'producer', weight: 0.03 },
    ],
  },
  {
    root: 'rabbitmq',
    operation: 'consume orders.created',
    p50: 420,
    errorRate: 0.04,
    volume: 10,
    children: [
      {
        service: 'notification-api',
        operation: 'send order confirmation',
        kind: 'consumer',
        weight: 0.9,
        tags: { 'messaging.system': 'rabbitmq', 'messaging.operation.type': 'process' },
        children: [
          db('SELECT users', 0.15),
          { service: 'notification-api', operation: 'render template', kind: 'internal', weight: 0.2 },
          { service: 'notification-api', operation: 'SMTP send', kind: 'client', weight: 0.55, blame: 4 },
        ],
      },
    ],
  },
  {
    root: 'cdn-edge',
    operation: 'GET /assets/*',
    p50: 38,
    errorRate: 0.003,
    volume: 26,
    children: [
      cache('GET asset:etag', 0.3),
      { service: 'image-resizer', operation: 'resize on miss', kind: 'client', weight: 0.6, chance: 0.2, blame: 1 },
    ],
  },
  {
    root: 'catalog-api',
    operation: 'reindex catalog',
    p50: 3100,
    errorRate: 0.07,
    volume: 3,
    children: [
      db('SELECT products FOR UPDATE', 0.3),
      { service: 'elasticsearch', operation: 'POST _bulk', kind: 'client', weight: 0.55, blame: 4, tags: { 'db.system.name': 'elasticsearch' } },
      cache('FLUSHDB catalog', 0.04),
    ],
  },
  {
    root: 'pricing',
    operation: 'GET /quote',
    p50: 96,
    errorRate: 0.011,
    volume: 16,
    children: [
      cache('GET price:*', 0.18),
      { service: 'currency', operation: 'Convert EUR→USD', kind: 'client', weight: 0.4, blame: 2 },
      { service: 'pricing', operation: 'resolve tax band', kind: 'internal', weight: 0.25 },
    ],
  },
];

/** Scenario index drawn against traffic share, so volumes are not uniform. */
const WEIGHTED_SCENARIOS: number[] = SCENARIOS.flatMap((scenario, index) =>
  Array.from({ length: scenario.volume }, () => index),
);

// ─── Trace generation ──────────────────────────────────────────────────────

function traceIdFor(index: number): string {
  // 32 hex chars, stable per index and visually unlike its neighbours.
  const a = hash(`tessera-trace-${index}`).toString(16).padStart(8, '0');
  const b = hash(`tessera-trace-${index}-b`).toString(16).padStart(8, '0');
  const c = hash(`${index}-tessera`).toString(16).padStart(8, '0');
  const d = hash(`${index}-fleet`).toString(16).padStart(8, '0');
  return `${a}${b}${c}${d}`.slice(0, 32);
}

function spanIdFor(traceId: string, ordinal: number): string {
  return hash(`${traceId}:${ordinal}`).toString(16).padStart(8, '0').concat(
    hash(`${ordinal}:${traceId}`).toString(16).padStart(8, '0'),
  ).slice(0, 16);
}

interface Plan {
  scenario: Scenario;
  durationMs: number;
  status: TraceStatus;
  startTime: number;
  /** Blame path: which chain of steps carries the failure. */
  failing: Set<string>;
}

/**
 * Choose the failing step by walking the tree and following the child with the
 * highest `blame` draw. A failure has a path — the root fails *because* a leaf
 * did — and stamping one random span with `error` produces traces where the
 * root is green and something deep inside is red, which never happens.
 */
function blamePath(children: Step[], next: () => number, path: string[] = []): string[] {
  const candidates = children.filter((step) => step.blame !== undefined);
  if (candidates.length === 0) return path;
  const total = candidates.reduce((sum, step) => sum + (step.blame ?? 0), 0);
  let draw = next() * total;
  for (const step of candidates) {
    draw -= step.blame ?? 0;
    if (draw <= 0) {
      const here = [...path, step.operation];
      return step.children ? blamePath(step.children, next, here) : here;
    }
  }
  return path;
}

function planTrace(index: number, now: number): Plan {
  const traceId = traceIdFor(index);
  const next = rng(hash(traceId));
  const scenario = SCENARIOS[
    pick(WEIGHTED_SCENARIOS, next()) as number
  ] as Scenario;

  const durationMs = Math.max(4, Math.round(scenario.p50 * jitter(next(), 0.7)));
  const failed = next() < scenario.errorRate;
  // Traffic thins out overnight, so the draw is biased toward recent hours —
  // which is also what makes "last 1h" a useful default. The exponent was 2,
  // and that skewed so hard that within a single hour the volume chart showed a
  // cliff rising to "now" rather than a shape. 1.35 plus a slow wobble reads as
  // traffic instead of as an artefact of the generator.
  const wobble = 0.85 + 0.3 * Math.sin(next() * Math.PI * 2);
  const ago = Math.round(Math.min(next() ** 1.35 * wobble, 1) * 24 * HOUR);

  return {
    scenario,
    durationMs,
    status: failed ? 'error' : 'ok',
    startTime: now - ago,
    failing: new Set(failed ? blamePath(scenario.children, next) : []),
  };
}

interface Built {
  spans: Span[];
  logs: LogEntry[];
  markers: LogMarker[];
  dependencyGraph: DependencyGraph;
}

function buildSpans(traceId: string, plan: Plan): Span[] {
  const next = rng(hash(`${traceId}:spans`));
  const spans: Span[] = [];
  let ordinal = 0;

  const rootId = spanIdFor(traceId, ordinal++);
  const rootFailed = plan.status === 'error';
  spans.push({
    spanId: rootId,
    parentSpanId: null,
    service: plan.scenario.root,
    operation: plan.scenario.operation,
    startTime: plan.startTime,
    durationMs: plan.durationMs,
    status: plan.status,
    kind: plan.scenario.operation.startsWith('consume') ? 'consumer' : 'server',
    resource: resourceFor(plan.scenario.root),
    tags: rootFailed ? { 'http.response.status_code': '500' } : { 'http.response.status_code': '200' },
    events: [],
  });

  const walk = (steps: Step[], parentId: string, parentStart: number, parentDuration: number) => {
    // Children are laid out in order inside the parent's window, each taking its
    // weighted share with a small gap. Sequential rather than overlapping: this
    // is the shape a synchronous request actually has, and it is what makes the
    // waterfall's staircase readable.
    let cursor = parentStart + Math.max(1, Math.round(parentDuration * 0.01));
    const budget = parentStart + parentDuration;

    for (const step of steps) {
      if (step.chance !== undefined && next() > step.chance) continue;
      const duration = Math.max(1, Math.round(parentDuration * step.weight * jitter(next(), 0.35)));
      if (cursor >= budget) break;

      const id = spanIdFor(traceId, ordinal++);
      const failing = plan.failing.has(step.operation);
      spans.push({
        spanId: id,
        parentSpanId: parentId,
        service: step.service,
        operation: step.operation,
        startTime: cursor,
        durationMs: Math.min(duration, budget - cursor),
        status: failing ? 'error' : 'ok',
        kind: step.kind,
        resource: resourceFor(step.service),
        tags: {
          ...(step.tags ?? {}),
          ...(failing ? { 'error.type': errorTypeFor(step.service) } : {}),
        },
        events: failing
          ? [
              {
                time: cursor + duration - 2,
                name: 'exception',
                attributes: {
                  'exception.type': errorTypeFor(step.service),
                  'exception.message': errorMessageFor(step.service, step.operation),
                },
              },
            ]
          : [],
      });

      if (step.children) walk(step.children, id, cursor, duration);
      cursor += duration + Math.round(parentDuration * 0.005);
    }
  };

  walk(plan.scenario.children, rootId, plan.startTime, plan.durationMs);
  return spans;
}

function resourceFor(service: string): Resource {
  return (
    FLEET_RESOURCES[service] ?? {
      serviceName: service,
      serviceNamespace: 'unknown',
      deploymentEnvironment: 'staging',
      attributes: {},
    }
  );
}

function errorTypeFor(service: string): string {
  if (service === 'stripe' || service === 'paypal') return 'TimeoutRejectedException';
  if (service === 'postgres') return 'NpgsqlException';
  if (service === 'elasticsearch') return 'TransportException';
  if (service === 'redis') return 'RedisConnectionException';
  return 'HttpRequestException';
}

function errorMessageFor(service: string, operation: string): string {
  if (service === 'stripe' || service === 'paypal') return 'HTTP request timed out after 8000ms';
  if (service === 'postgres') return `deadlock detected during ${operation}`;
  if (service === 'elasticsearch') return 'circuit_breaking_exception: parent data too large';
  if (service === 'redis') return 'connection reset by peer';
  return `upstream returned 503 for ${operation}`;
}

// ─── Logs ──────────────────────────────────────────────────────────────────

const CHATTER: Record<string, string[]> = {
  server: ['request accepted', 'route matched', 'response written'],
  client: ['upstream call issued', 'connection pooled', 'response deserialized'],
  internal: ['stage entered', 'stage completed'],
  producer: ['message published'],
  consumer: ['message received', 'ack sent'],
  unspecified: ['work started'],
};

function buildLogs(traceId: string, spans: Span[]): LogEntry[] {
  const next = rng(hash(`${traceId}:logs`));
  const entries: LogEntry[] = [];

  for (const span of spans) {
    const failing = span.status === 'error';
    // Healthy spans are quiet — most spans emit nothing, which is what makes a
    // log stream readable and what makes the failing ones stand out.
    if (!failing && next() > 0.32) continue;

    const chatter = CHATTER[span.kind] ?? CHATTER['internal'] ?? [];
    const level: LogLevel = failing ? 'error' : next() > 0.86 ? 'debug' : 'info';

    entries.push({
      timestamp: span.startTime + Math.round(span.durationMs * 0.3),
      level,
      service: span.service,
      traceId,
      spanId: span.spanId,
      message: failing
        ? `${span.operation} failed · ${errorMessageFor(span.service, span.operation)}`
        : `${span.operation} · ${pick(chatter, next())}`,
      fields: failing
        ? { 'error.type': errorTypeFor(span.service), 'span.kind': span.kind }
        : { 'span.kind': span.kind },
    });

    if (failing) {
      // A failure is rarely one line: the retry attempts precede it, and their
      // presence is what tells an operator the client did try.
      entries.push({
        timestamp: span.startTime + Math.round(span.durationMs * 0.15),
        level: 'warn',
        service: span.service,
        traceId,
        spanId: span.spanId,
        message: `retry 1/3 on ${span.operation}`,
        fields: { 'retry.attempt': '1', 'retry.max': '3' },
      });
    }
  }

  return entries.sort((a, b) => a.timestamp - b.timestamp);
}

function buildDependencies(spans: Span[]): DependencyGraph {
  const byId = new Map(spans.map((span) => [span.spanId, span]));
  const nodes = new Map<string, { id: string; name: string; kind: 'service' | 'database' | 'external' }>();
  const edges = new Map<string, { fromId: string; toId: string; callCount: number; errorCount: number }>();

  const kindOf = (service: string): 'service' | 'database' | 'external' => {
    const namespace = NAMESPACES[service];
    if (namespace === 'data') return 'database';
    if (namespace === 'payments') return 'external';
    return 'service';
  };

  for (const span of spans) {
    nodes.set(span.service, { id: span.service, name: span.service, kind: kindOf(span.service) });
    const parent = span.parentSpanId ? byId.get(span.parentSpanId) : undefined;
    if (!parent || parent.service === span.service) continue;

    const key = `${parent.service}→${span.service}`;
    const existing = edges.get(key);
    if (existing) {
      existing.callCount += 1;
      existing.errorCount += span.status === 'error' ? 1 : 0;
    } else {
      edges.set(key, {
        fromId: parent.service,
        toId: span.service,
        callCount: 1,
        errorCount: span.status === 'error' ? 1 : 0,
      });
    }
  }

  return { nodes: [...nodes.values()], edges: [...edges.values()] };
}

// ─── Public surface ────────────────────────────────────────────────────────

// 900, not 220. A 24-hour fleet of 220 leaves ~35 traces in the default hour,
// which is a real enough table and far too thin for the volume mosaic — most
// buckets held nothing and the chart read as broken rather than as quiet.
const FLEET_SIZE = 900;

export function buildFleet(now: number): {
  traces: TraceSummary[];
  detail: (traceId: string) => Built | undefined;
  services: ServiceSummary[];
  red: Record<string, ServiceRedResponse>;
  logStream: LogEntry[];
} {
  const plans = new Map<string, Plan>();
  const traces: TraceSummary[] = [];

  for (let index = 0; index < FLEET_SIZE; index += 1) {
    const traceId = traceIdFor(index);
    const plan = planTrace(index, now);
    plans.set(traceId, plan);
  }

  // Span trees are built eagerly once: the service inventory, the RED figures
  // and the global log stream all have to agree with what a trace shows when it
  // is opened, and deriving them from the same spans is the only way that holds.
  const built = new Map<string, Built>();
  for (const [traceId, plan] of plans) {
    const spans = buildSpans(traceId, plan);
    const logs = buildLogs(traceId, spans);
    built.set(traceId, {
      spans,
      logs,
      markers: logs
        .filter((entry) => entry.level === 'warn' || entry.level === 'error')
        .map((entry) => ({
          spanId: entry.spanId,
          offsetMs: entry.timestamp - plan.startTime,
          level: entry.level,
          message: entry.message,
          timestampUnixMs: entry.timestamp,
        })),
      dependencyGraph: buildDependencies(spans),
    });

    traces.push({
      traceId,
      rootService: plan.scenario.root,
      rootOperation: plan.scenario.operation,
      startTime: plan.startTime,
      durationMs: plan.durationMs,
      status: plan.status,
      spanCount: spans.length,
      services: [...new Set(spans.map((span) => span.service))],
    });
  }

  // ─ Inventory and RED, aggregated from the spans above ─
  const counters = new Map<string, { spans: number; errors: number; totalMs: number; durations: number[]; ops: Map<string, number> }>();
  for (const { spans } of built.values()) {
    for (const span of spans) {
      const counter = counters.get(span.service) ?? {
        spans: 0,
        errors: 0,
        totalMs: 0,
        durations: [],
        ops: new Map<string, number>(),
      };
      counter.spans += 1;
      counter.errors += span.status === 'error' ? 1 : 0;
      counter.totalMs += span.durationMs;
      counter.durations.push(span.durationMs);
      counter.ops.set(span.operation, (counter.ops.get(span.operation) ?? 0) + 1);
      counters.set(span.service, counter);
    }
  }

  const services: ServiceSummary[] = [...counters.entries()]
    .map(([name, counter]) => ({
      name,
      // Scaled up: the sample is 220 traces, a real hour is thousands. The ratio
      // is what the screen reads, and a service inventory showing 41 spans in an
      // hour looks like a broken collector rather than a quiet service.
      spanCount: counter.spans * 9,
      errorCount: counter.errors * 9,
      operations: [...counter.ops.entries()]
        .sort((a, b) => b[1] - a[1])
        .map(([operation, count]) => ({ name: operation, count: count * 9 })),
    }))
    .sort((a, b) => b.spanCount - a.spanCount);

  const red: Record<string, ServiceRedResponse> = {};
  for (const [name, counter] of counters) {
    const sorted = [...counter.durations].sort((a, b) => a - b);
    const p95 = sorted[Math.min(sorted.length - 1, Math.floor(sorted.length * 0.95))] ?? 0;
    // Databases and caches are scraped as span counters, not as RED metrics —
    // so they honestly report no rate and no p95, which is the case the UI has
    // to render as an em dash rather than as a zero.
    const instrumented = NAMESPACES[name] !== 'data';
    red[name] = {
      requestRatePerSec: instrumented ? Number(((counter.spans * 9) / 3600).toFixed(1)) : null,
      errorRatio: counter.spans > 0 ? counter.errors / counter.spans : 0,
      durationP95Ms: instrumented ? p95 : null,
      source: instrumented ? 'metrics' : 'spanApprox',
    };
  }

  const logStream = [...built.values()]
    .flatMap((entry) => entry.logs)
    .sort((a, b) => a.timestamp - b.timestamp);

  return {
    traces,
    detail: (traceId) => built.get(traceId),
    services,
    red,
    logStream,
  };
}
