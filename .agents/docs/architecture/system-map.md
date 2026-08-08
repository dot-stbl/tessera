# Tessera — system map (living)

> **Status:** living doc. Update when modules/contracts/phases land.  
> **Tip:** `develop` @ `1eb5da9`. After each phase, bump “Last updated” + commit table.  
> **ADRs:** `0001` platform · `0002` observability model · `0003` module cut  
> **Plans:** `.agents/plans/mvp-2-core/P5`…`P9`

**Last updated:** 2026-08-08 (wire JSON + route templates + Vironima backends; P5–P9 still complete)

---

## 1. What Tessera is

Self-hosted **APM UI / read-model** over external telemetry backends (first: Victoria Traces/Logs/Metrics).  
**Does not store telemetry** (ADR-0001 D8). Domain is **OpenTelemetry-shaped** (ADR-0002).  
Providers only map `wire → OTel`; smart logic lives **above** them (Analysis + modules).

---

## 2. Solution layers

```
web/                    FE (React) — hand mocks / evolving; not blocking BE
src/host/Tessera.Host   composition root only (DI + pipeline)
src/shared/
  Tessera.Shared.Kernel     domain + provider interfaces + Analysis (pure)
  Tessera.Shared.Http       Refit + resilience
  Tessera.Shared.Validation shell
  Tessera.Banner            CLI banner (not Shared.* cap)
  infra/
    Authentication          Guest / AdminBearer / LDAP / Keycloak
    Web                     ProblemDetails + OpenAPI + Scalar
    Storage                 Repository<T> + Specification (EF base)
    Telemetry               Tessera’s own OTel export
src/modules/            vertical slices (HTTP + orchestration) — cap 5 flat
src/providers/          Victoria (wire → domain)
tests/unit/             Shared · Modules · Providers · Architecture
```

**Dependency rule:** `modules` → `shared` only · `providers` → `shared` only ·  
`host` → everything · **modules ↛ providers** (NetArchTest).

**Smart logic placement (ADR-0003 A3):**

| Kind | Where |
|------|--------|
| Pure derivation | `Kernel/Analysis/**` |
| Orchestration (N providers) | Module `Services/` |
| `wire → OTel` | Provider mappers |

---

## 3. Modules — responsibility matrix

| Module | Owns | Does **not** own |
|--------|------|------------------|
| **Traces** | Trace search, request-view (`trace_id` union), log markers, **errors inbox**, per-trace deps (P9) | RED inventory, global map |
| **Logs** | Log query by `traceId` (MVP correlation) | Trace assembly |
| **Discovery** | Service inventory, **RED** per service | Errors inbox, request-view |
| **Health** | Composite provider health | Business APM |
| **Preferences** | User prefs (SQLite) | Telemetry |

No `Modules.Metrics` / `Modules.Errors` / `Modules.Projects` until nest (ADR-0003 A1).

---

## 4. Kernel Analysis (pure)

| Area | Types / API | Used by |
|------|-------------|---------|
| **Request view** | `RequestView`, `RequestViewMode`, `LogMarker`, `RequestViewAnalysis` | Traces |
| **Errors** | `SpanError`, `ErrorAnalysis` (IsError, exception extract, normalize, group key) | Traces |
| **RED** | `RedSnapshot`, `RedSource`, `RedMetricExtract` | Discovery |
| **Deps (P9)** | `DependencyGraph`, `DependencyNode`/`Edge`, `DependencyAnalysis.FromTrace` | Traces |

---

## 5. Providers

### Interfaces (`Kernel/Providers/`)

| Interface | Methods (summary) |
|-----------|-------------------|
| `ITraceProvider` | `SearchAsync`, `GetByIdAsync` |
| `ILogProvider` | `QueryAsync`, `ListByTraceAsync` |
| `IDiscoveryProvider` | `ListServicesAsync` |
| `IHealthProvider` | health reports |
| `IMetricsProvider` | `QueryRangeAsync`, `QueryInstantAsync`, `LabelValuesAsync` |

### Victoria (`Tessera.Providers.Victoria`)

| Impl | Backend |
|------|---------|
| Trace / Log / Discovery / Health | VT Jaeger API, VL LogsQL, inventory, probes |
| Metrics | PromQL on VM (`/prometheus/api/v1/*`); `UnconfiguredMetricsProvider` if no URL |
| Mappers | OTel-ify: SpanKind, Resource, severity, µs→ms, exception events |

DI: **only** inside `AddVictoriaProvider` / `VictoriaServicesRegistration` (not Program.cs).

---

## 6. HTTP contracts (`/api/v1`)

Single source: `Tessera.Shared.Kernel.Api.ApiRoutes`.  
Wire times: **UTC unix ms** (`*UnixMs`). Errors: **RFC 9457** ProblemDetails.

### Wire JSON (`TesseraJsonOptions`)

Single definition: `Tessera.Shared.Kernel.Api.TesseraJsonOptions` — applied to MVC (`AddJsonOptions`) and to `TesseraExceptionHandler` (ProblemDetails body). Do not invent a second options set.

| Rule | Wire shape |
|------|------------|
| Property names | **camelCase** (`PropertyNamingPolicy` + dictionary keys) |
| Enums | **camelCase strings** via `JsonStringEnumConverter(JsonNamingPolicy.CamelCase)` — e.g. `"ok"`, `"healthy"`, `"full"`, `"metrics"` |
| `TraceId` / `SpanId` | **bare JSON strings** (`"abc…"`), not `{ "value": "…" }` — `TraceIdJsonConverter` / `SpanIdJsonConverter` |
| `LogLevel` | **`info` / `warn`** (and `trace`/`debug`/`error`/`fatal`) — enum members are OTel-style `Info`/`Warn`, not .NET `Information`/`Warning` |

### Route templates — relative vs absolute

Controllers use **class `[Route]` + relative method templates**. Absolute constants are for docs/tests/link generation only.

| Constant | Value | Use |
|----------|-------|-----|
| `ApiRoutes.Traces` | `api/v1/traces` | `[Route]` on `TracesController` |
| `ApiRoutes.TraceByIdRelative` | `{traceId:length(32)}` | `[HttpGet]` on that controller |
| `ApiRoutes.Trace` | `api/v1/traces/{traceId:length(32)}` | **docs / tests / links only** |
| `ApiRoutes.TraceLogsRelative` | `{traceId:length(32)}/logs` | relative method (if used on Traces group) |
| `ApiRoutes.TraceLogs` | absolute path | docs / tests only |

Putting `ApiRoutes.Trace` on a method under `[Route(ApiRoutes.Traces)]` double-prefixes (`api/v1/traces/api/v1/traces/{id}`) — that bug is why `*Relative` exists.

### Health — `Modules.Health`

| Method | Route | Response |
|--------|-------|----------|
| GET | `/api/v1/health` | composite provider health (200/503) |

### Discovery — `Modules.Discovery`

| Method | Route | Query | Response |
|--------|-------|-------|----------|
| GET | `/api/v1/services` | — | `ServiceSummary[]` (`name`, `spanCount`, `errorCount`, `operations[]`) |
| GET | `/api/v1/services/{serviceName}/red` | `startUnixMs`, `endUnixMs`, `operation?`, `stepSeconds?` | `ServiceRedResponse`: `requestRatePerSec?`, `errorRatio?`, `durationP95Ms?`, `source`: `metrics` \| `spanApprox` (camelCase enum) |

**RED behavior:** PromQL first (OTel HTTP histogram metrics); on metrics failure → SpanApprox from inventory (ratio only; rate/p95 null).

### Traces — `Modules.Traces`

| Method | Route | Query / notes | Response |
|--------|-------|---------------|----------|
| GET | `/api/v1/traces` | service, operation, start/end, duration, limit, cursor | `Page<TraceSummary>` |
| GET | `/api/v1/traces/{traceId}` | 32-char hex; controller: `TraceByIdRelative` | **Request view** `GetTraceResponse` (below) |
| GET | `/api/v1/errors` | `startUnixMs`, `endUnixMs`, `service?`, `limit?` | `ErrorGroupSummary[]` |

#### `GetTraceResponse` (request view)

| Field | Meaning |
|-------|---------|
| `trace` | `TraceDetail?` (null in logs-only mode) |
| `correlatedLogs` | `LogEntry[]` |
| `mode` | `full` \| `spansOnly` \| `logsOnly` \| `empty` (camelCase enum) |
| `markers` | log markers for waterfall (`spanId?`, `offsetMs`, level, …) |
| `erroredSpanCount` | spans with `status == error` |
| `exceptions` | `{ exceptionType?, exceptionMessage?, spanId, service, operation }[]` |
| `dependencyGraph` | `DependencyGraph?` — per-trace mini-map (`null` logs-only; empty when no CLIENT deps) |

#### `DependencyGraph` (per-trace)

```json
{
  "nodes": [
    { "id": "checkout-api", "name": "checkout-api", "kind": "service" },
    { "id": "currency", "name": "currency", "kind": "service" },
    { "id": "db:postgresql:orders", "name": "orders", "kind": "database" },
    { "id": "external:stripe", "name": "stripe", "kind": "external" }
  ],
  "edges": [
    { "fromId": "checkout-api", "toId": "currency", "callCount": 1, "errorCount": 0 },
    { "fromId": "checkout-api", "toId": "db:postgresql:orders", "callCount": 2, "errorCount": 0 },
    { "fromId": "checkout-api", "toId": "external:stripe", "callCount": 1, "errorCount": 1 }
  ]
}
```

- **Service** nodes: `id` = `service.name` (Resource).
- **Database** synthetic: `db:{db.system}` or `db:{db.system}:{db.name}`.
- **External** synthetic: `external:{peer.service|server.address}`.
- **ErrorCount**: CLIENT span `Status == Error` only (child SERVER status ignored).
- Node `kind` on the wire is camelCase enum (`service` / `database` / `external`).

**404 rules:** only when **both** spans and logs empty. Log-only / spans-only → **200** + `mode`.

#### `ErrorGroupSummary`

`key`, `exceptionType?`, `message?`, `count`, `sampleTraceIds[]` (max 5).  
MVP: only traces with **root** `Status == Error`; detail fetch cap **20**.

### Logs — `Modules.Logs`

| Method | Route | Query | Response |
|--------|-------|-------|----------|
| GET | `/api/v1/logs` | `traceId` (required MVP), time range, limit | log page / list |

Log entries: `level` is `info` / `warn` / … (see Wire JSON), not `information` / `warning`.

### OpenAPI / Scalar

- Doc: `GET /openapi/v1.json`
- UI: `GET /scalar/v1`

---

## 7. Domain model (Kernel, OTel-shaped)

| Type | Notes |
|------|--------|
| `Span` | id, parent, Service, Operation, StartTime (ms), DurationMs, Status, Tags, Events, **Kind**, **Resource** |
| `TraceDetail` / `TraceSummary` | root service/op, status, spans / counts; ids as `TraceId`/`SpanId` domain wrappers |
| `LogEntry` | timestamp, level (`LogLevel`: Info/Warn/…), service, traceId?, spanId?, message, fields |
| `Resource` | service.name + attrs |
| `Metric*` | sample/series/matrix/vector for PromQL results |
| `ServiceSummary` | inventory + span/error counts |

---

## 8. Phase progress (core track)

| Phase | Deliverable | Tip commits (approx) | Status |
|-------|-------------|----------------------|--------|
| **P5** | OTel domain + Victoria mappers + `IMetricsProvider` | `c7f1e6c`…`cf9a58f` | ✅ |
| **P6** | Request-view degrade + markers | `00dfd8d`, `d039da7` | ✅ |
| **P7** | Errors analysis + inbox + RV fields | `f489ccd`, `03c8be5` | ✅ |
| **P8** | Service RED (metrics + SpanApprox) | `466f6fa`, `58eb1e4` | ✅ |
| **P9** | Per-trace dependency mini-map | `33f6a49`, `9d3b434` (+ docs) | ✅ |
| post-core | Projects, rollup cache, global map | — | ⏸ |

Wire-format unification (enums, ids, relative routes): `fc84c88` on `develop` (after P9).

---

## 9. Config / ops (short)

- TOML: `tessera.toml` + optional local; secrets `env:` / `file:`
- Victoria sections: `[victoria.traces|logs|metrics]`
- Auth: guest + admin bearer + LDAP/Keycloak (enabled in TOML)
- Storage: SQLite prefs via `IFileSystemLayout`
- Ports: host **1990**, Vite **1991**

---

## 10. Vironima live backends (reference)

In-cluster URLs on **vironima.internal** (no secrets). Tessera is a **read UI** over these; it is **not** on the OTLP path.

| Signal | In-cluster base | Tessera protocol |
|--------|-----------------|------------------|
| **Metrics** | `http://vmsingle-victoriametrics-victoria-metrics-k8s-stack.telemetry.svc:8428` | PromQL (`/prometheus/api/v1/*`) |
| **Logs** | `http://victorialogs.telemetry.svc:9428` | LogsQL |
| **Traces** | `http://victoriatraces.telemetry.svc:10428` | **Jaeger** API under `/select/jaeger/...` |

**Grafana vs Tessera on traces:** Grafana datasource uses Tempo-compatible `/select/tempo`; Tessera uses **Jaeger** `/select/jaeger`. Same VT process, different select APIs.

**Public UI (cluster ingress):** vmui / vlogs / vtraces / grafana under `*.vironima.internal`.

**OTLP path (apps → storage):** apps → **otel-collector** → Victoria Traces / Logs / Metrics. Tessera only **queries** VT/VL/VM; it does not receive OTLP.

Configure Tessera with the in-cluster bases above in `[victoria.traces|logs|metrics]` (or port-forward / ingress equivalents for local dev).

---

## 11. How to update this doc

After a phase lands on `develop`:

1. Bump **Last updated** + tip SHA.
2. Refresh §3–§6 if routes/fields/wire format changed.
3. Tick §8 phase row + commit hashes.
4. Commit: `[.stbl](feat/docs): system-map — …` (example).

**Related:** `core-design.md` (semantics) · `modules.md` (older; prefer this map when drift) · ADRs.
