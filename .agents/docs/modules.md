# Tessera — Modules

> Per-module contracts. Each module is a self-contained vertical slice in
> `src/modules/Tessera.Modules.<Feature>/`. Modules don't reference each other
> directly — communication via interfaces in `Tessera.Shared.*` or via
> composition root in `Tessera.Host`.

## Module catalog (MVP)

| Module | Purpose | Status |
|--------|---------|--------|
| `Tessera.Modules.Traces` | Trace list + detail (waterfall), VT Refit client | MVP |
| `Tessera.Modules.Logs` | Logs by trace_id, VL Refit client | MVP |
| `Tessera.Modules.Discovery` | Service inventory aggregator | MVP |
| `Tessera.Modules.Health` | /health endpoint + per-Victoria checks | MVP |
| `Tessera.Modules.Metrics` | RED metrics per service, VM Refit client | Stretch |
| `Tessera.Modules.ServiceMap` | Service dependency graph | Stretch |
| `Tessera.Modules.Alerts` | Alert rule management | Out |

---

## Provider abstraction (2026-07-19)

Modules **do not** own Refit clients directly. They consume provider
**interfaces** from `Tessera.Shared.Kernel`:

- `ITraceProvider` — search + get-by-id
- `ILogProvider` — query + list-by-trace
- `IDiscoveryProvider` — list services
- `IHealthProvider` — health check

Concrete provider implementations live in `src/providers/Tessera.Providers.<Name>/`.
**MVP-01:** `Tessera.Providers.Victoria` implements all 4 interfaces using
Refit clients to VT/VL (VM wired but unused — Metrics module deferred).
Victoria-specific Refit interfaces + Jaeger/LogsQL DTO mapping live in
the provider, not in modules.

Wiring happens in `Tessera.Host/Program.cs` via `AddVictoriaProvider(...)`.

This means:
- **Module is backend-agnostic** — tests use NSubstitute mocks for
  `ITraceProvider`, no VT container needed for unit tests.
- **Adding a second backend (Tempo/Jaeger/Loki in MVP-02+)** = new
  `Tessera.Providers.<Name>` project implementing the same interfaces.
  Zero changes to modules.
- **Architecture test `Tessera.ArchitectureTests.NoModuleReferencesProviders`**
  enforces isolation — modules cannot have `<ProjectReference>` on
  `Tessera.Providers.*`.

---

## Tessera.Modules.Traces

### Responsibility

Fetch and expose trace data via the `ITraceProvider` abstraction. Backend-
agnostic — `Tessera.Providers.Victoria` provides the concrete impl for
MVP-01; future providers (Tempo, Jaeger) plug in without changes.

### Owns

- Handlers: `ListTracesHandler`, `GetTraceHandler` (with span tree reconstruction + log correlation via `ILogProvider`)
- Endpoint group: `MapTracesEndpoints`
- HTTP models (DTOs): `TraceSummary`, `TraceDetail`, `ListTracesRequest`, `ListTracesResponse`
- DI: `AddTracesModule(IServiceCollection)`

### Consumes

- `ITraceProvider` — trace search + get-by-id (from `Tessera.Shared.Kernel`)
- `ILogProvider` — log correlation in `GetTraceHandler` (separate call, NOT inside `ITraceProvider`)

### Public surface (HTTP)

| Method | Path | Purpose |
|--------|------|---------|
| `GET` | `/api/traces` | List traces (search) |
| `GET` | `/api/traces/{traceId}` | Get trace detail |

#### `GET /api/traces`

**Query params:**

| Param | Type | Required | Notes |
|-------|------|----------|-------|
| `service` | string | optional | Filter by service |
| `operation` | string | optional | Filter by operation |
| `start` | int (unix ms) | **required** | Start of time range |
| `end` | int (unix ms) | **required** | End of time range |
| `minDurationMs` | int | optional | Min duration in ms |
| `maxDurationMs` | int | optional | Max duration in ms |
| `cursor` | string | optional | Pagination cursor |
| `limit` | int | optional | Default 50, max 200 |

**Response:**

```json
{
  "items": [
    {
      "traceId": "abc123def456",
      "rootService": "checkout-api",
      "rootOperation": "POST /checkout",
      "startTime": 1721337600000,
      "durationMs": 1247,
      "status": "error",
      "spanCount": 23,
      "services": ["checkout-api", "postgres", "stripe"]
    }
  ],
  "cursor": "abc123def456|1721337600000",
  "hasMore": true
}
```

#### `GET /api/traces/{traceId}`

**Response:**

```json
{
  "traceId": "abc123def456",
  "rootService": "checkout-api",
  "rootOperation": "POST /checkout",
  "startTime": 1721337600000,
  "durationMs": 1247,
  "status": "error",
  "spans": [
    {
      "spanId": "root",
      "parentSpanId": null,
      "service": "checkout-api",
      "operation": "POST /checkout",
      "startTime": 1721337600000,
      "durationMs": 1247,
      "status": "error",
      "tags": { "http.method": "POST", "http.status_code": "500" },
      "events": [
        {
          "time": 1721337600500,
          "name": "exception",
          "attributes": { "exception.type": "StripeError" }
        }
      ],
      "children": [
        {
          "spanId": "db1",
          "parentSpanId": "root",
          "service": "postgres",
          "operation": "SELECT orders",
          "startTime": 1721337600100,
          "durationMs": 50,
          "status": "ok",
          "tags": { "db.statement": "SELECT * FROM orders WHERE ..." },
          "children": []
        }
      ]
    }
  ],
  "warnings": null
}
```

### Dependencies

- `Tessera.Shared.Kernel` — `Result`, `Error`, `TimeRange`, `Id`
- `Tessera.Shared.Http` — Refit base, resilience
- `Tessera.Shared.Telemetry` — OTel tracing for HTTP calls

### Cross-module

- **`Tessera.Modules.Logs`** — `ListTraceLogsHandler` needs logs for a trace;
  calls `IVictoriaLogsClient` (registered by Logs module via `Tessera.Shared.Http`).

### Tests

- `Tessera.Modules.Traces.Unit/`:
  - `GetTraceHandlerTests` — verify span tree reconstruction from flat references
  - `ListTracesHandlerTests` — verify query param mapping
  - `IVictoriaTracesClient` mocked via NSubstitute
- `Tessera.Modules.Traces.Unit/` integration: full request/response via WebApplicationFactory

---

## Tessera.Modules.Logs

### Responsibility

Fetch and expose logs via the `ILogProvider` abstraction. Backend-agnostic
— Victoria LogsQL is the MVP-01 backend; future providers (Loki) plug in
without changes.

### Owns

- Handlers: `ListLogsByTraceHandler`, `ListLogsHandler` (stretch: arbitrary LogsQL passthrough)
- Endpoint group: `MapLogsEndpoints`
- HTTP models (DTOs): `LogEntryDto`, `ListLogsRequest`, `LogsResponse`
- DI: `AddLogsModule(IServiceCollection)`

### Consumes

- `ILogProvider` — log query + list-by-trace (from `Tessera.Shared.Kernel`)

### Public surface (HTTP)

| Method | Path | Purpose |
|--------|------|---------|
| `GET` | `/api/logs` | Query logs by trace_id (MVP) or ad-hoc LogsQL (stretch) |

#### `GET /api/logs?trace_id=...`

**Query params:**

| Param | Type | Required | Notes |
|-------|------|----------|-------|
| `trace_id` | string | **required** | Trace ID (in MVP — only mode) |
| `start` | int (unix ms) | optional | Default: trace start time (when known) |
| `end` | int (unix ms) | optional | Default: trace end time (when known) |
| `limit` | int | optional | Default 500, max 1000 |

**Response:**

```json
{
  "entries": [
    {
      "timestamp": 1721337600400,
      "level": "ERROR",
      "service": "checkout-api",
      "traceId": "abc123def456",
      "spanId": "span_001",
      "message": "failed to charge card",
      "fields": {
        "error.kind": "StripeError",
        "user_id": "u_42"
      }
    }
  ],
  "total": 1
}
```

### LogsQL construction (tessera side)

Backend builds LogsQL query from request params:

```csharp
// trace_id correlation
var query = $"trace_id:\"{traceId}\"";
if (spanId is not null)
    query += $" AND span_id:\"{spanId}\"";

// add time range
var startStr = DateTimeOffset.FromUnixTimeMilliseconds(start).ToString("o");
var endStr = DateTimeOffset.FromUnixTimeMilliseconds(end).ToString("o");

// call vlselect
var response = await client.QueryAsync(query, limit, start: startStr, end: endStr);
```

### Dependencies

- `Tessera.Shared.Kernel` — `Result`, `Error`, `TimeRange`
- `Tessera.Shared.Http` — Refit base
- `Tessera.Shared.Telemetry` — OTel

### Cross-module

- **`Tessera.Modules.Traces`** — `TraceDetail` includes `traceId`, `startTime`, `durationMs` so
  Logs module can compute time range for log query.

### Tests

- `Tessera.Modules.Logs.Unit/`:
  - `ListLogsByTraceHandlerTests` — verify LogsQL construction
  - NDJSON parsing tests (line-delimited response)
  - Time format conversion (unix ms ↔ RFC3339)

---

## Tessera.Modules.Discovery

### Responsibility

Aggregate service inventory via the `IDiscoveryProvider` abstraction.
MVP-01 backend merges VT services + VL `_stream` values; future providers
plug in.

### Owns

- Handlers: `ListServicesHandler`, `GetServiceHandler`
- Endpoint group: `MapDiscoveryEndpoints`
- HTTP models (DTOs): `ServiceSummary`, `OperationSummary`
- DI: `AddDiscoveryModule(IServiceCollection)`

### Consumes

- `IDiscoveryProvider` — service inventory (from `Tessera.Shared.Kernel`)

### Public surface (HTTP)

| Method | Path | Purpose |
|--------|------|---------|
| `GET` | `/api/services` | List services with span counts |
| `GET` | `/api/services/{service}` | Single service with operations |

#### `GET /api/services`

**Query params:**

| Param | Type | Required | Notes |
|-------|------|----------|-------|
| `start` | int (unix ms) | **required** | Start of time range |
| `end` | int (unix ms) | **required** | End of time range |

**Response:**

```json
{
  "items": [
    {
      "name": "checkout-api",
      "spanCount": 8421,
      "errorCount": 23,
      "operations": [
        { "name": "POST /checkout", "count": 4210 },
        { "name": "GET /cart", "count": 2100 },
        { "name": "POST /payment", "count": 2111 }
      ]
    },
    {
      "name": "postgres",
      "spanCount": 12000,
      "errorCount": 0,
      "operations": [
        { "name": "SELECT orders", "count": 8000 },
        { "name": "INSERT payment", "count": 4000 }
      ]
    }
  ]
}
```

**How:** Backend calls `vt /select/jaeger/api/services` for list, then
`vt /select/jaeger/api/services/<svc>/operations` for each in parallel.
Span counts derived from a separate `/select/jaeger/api/traces` aggregation
(deferred to stretch — MVP returns only service/operation lists).

### Dependencies

- `Tessera.Shared.Kernel` — `Result`, `Error`
- `Tessera.Shared.Http` — Refit base

### Tests

- `Tessera.Modules.Discovery.Unit/`:
  - `GetServicesHandlerTests` — verify parallel VT calls
  - Aggregation tests

---

## Tessera.Modules.Health

### Responsibility

Composite health check via the `IHealthProvider` abstraction. Aggregates
per-provider status (Healthy/Degraded/Unhealthy) into a single endpoint
response. MVP-01: starts in degraded state if Victoria is unreachable,
recovers automatically when probes succeed.

### Owns

- Handler: `HealthHandler` (aggregates `IHealthProvider.CheckAsync` results)
- Endpoint group: `MapHealthEndpoints`
- DI: `AddHealthModule(IServiceCollection)`

### Consumes

- `IHealthProvider` — per-provider health probe (from `Tessera.Shared.Kernel`)

### Public surface (HTTP)

| Method | Path | Purpose |
|--------|------|---------|
| `GET` | `/api/health` | Per-backend health status |

#### `GET /api/health`

**Response (200 OK):**

```json
{
  "status": "healthy",
  "backends": {
    "vt": { "status": "ok", "latencyMs": 23, "version": "v0.1.0" },
    "vl": { "status": "ok", "latencyMs": 18, "version": "v0.5.0" },
    "vm": { "status": "ok", "latencyMs": 31, "version": "v1.95.0" }
  }
}
```

**Response (degraded, 200 OK still):**

```json
{
  "status": "degraded",
  "backends": {
    "vt": { "status": "ok", "latencyMs": 23 },
    "vl": { "status": "error", "error": "connection refused" },
    "vm": { "status": "ok", "latencyMs": 31 }
  }
}
```

**Response (503 Service Unavailable):**

When all backends are down.

### Probes

Each probe calls a minimal Victoria endpoint:

| Backend | Probe endpoint | Success criterion |
|---------|----------------|-------------------|
| vt | `GET /select/jaeger/api/services` | 200 + JSON |
| vl | `GET /select/logsql/query?query=*&limit=1` | 200 + NDJSON |
| vm | `GET /prometheus/api/v1/labels?limit=1` | 200 + JSON |

**Timeout:** 2 seconds per probe.

### Dependencies

- `Tessera.Shared.Kernel` — `Result`, `Error`
- `Tessera.Shared.Http` — Refit base (uses same `IVictoriaXClient` interfaces for probes)

### Tests

- `Tessera.Modules.Health.Unit/`:
  - All-OK scenario
  - One-down scenario (degraded)
  - All-down scenario (503)
  - Timeout scenarios

---

## Module communication pattern

**Hard rule:** modules do NOT reference each other.

```
❌ Tessera.Modules.Traces → Tessera.Modules.Logs   (cross-module import)
✅ Tessera.Modules.Traces → Tessera.Shared.Http     (uses IVictoriaLogsClient registered by Logs)
✅ Tessera.Host → Tessera.Modules.Traces + Logs    (composition root wires them)
```

**Pattern for trace-logs correlation:**

```
Tessera.Modules.Logs owns IVictoriaLogsClient (Refit).
Tessera.Modules.Logs.AddLogsModule(services) registers IVictoriaLogsClient in DI.

Tessera.Modules.Traces handlers that need logs receive IVictoriaLogsClient via DI.
Both modules reference Tessera.Shared.Http for the interface base.

Tessera.Host wires both:
  services.AddLogsModule();
  services.AddTracesModule();  // can use IVictoriaLogsClient from Logs
```

This keeps modules independent and testable.

## Stretch modules (planning notes)

### Tessera.Modules.Metrics

- `IVictoriaMetricsClient` — Refit to vmselect
- Endpoints: `/api/metrics/red?service=...&time_range=...`
- Backend: MetricsQL queries for rate/errors/duration
- UI: per-service panel
- New theme words: `kiln` (SLO), `relief` (alerts)

### Tessera.Modules.ServiceMap

- `IVictoriaDependenciesClient` — calls `/select/jaeger/api/dependencies`
- Endpoints: `/api/service-map?start=...&end=...`
- UI: graph view (visx or react-flow)

---

## Related docs

- `architecture.md` — overall architecture
- `victoria-stack.md` — VT/VL/VM API reference
- `../rules/coding/project-layers.md` — host/shared/modules structure
- `../rules/coding/project-deps-and-tests.md` — module dependency rules
