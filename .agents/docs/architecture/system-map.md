# Tessera — system map (living)

> **Status:** living doc. Update when modules/contracts/phases land.  
> **Tip:** `develop` @ `58eb1e4` (pre-P9). After each phase, bump “Last updated” + commit table.  
> **ADRs:** `0001` platform · `0002` observability model · `0003` module cut  
> **Plans:** `.agents/plans/mvp-2-core/P5`…`P9`

**Last updated:** 2026-08-07 (P5–P8 on `develop`; P9 in flight)

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
| **Deps (P9)** | `DependencyGraph` / edges (planned) | Traces |

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

### Health — `Modules.Health`

| Method | Route | Response |
|--------|-------|----------|
| GET | `/api/v1/health` | composite provider health (200/503) |

### Discovery — `Modules.Discovery`

| Method | Route | Query | Response |
|--------|-------|-------|----------|
| GET | `/api/v1/services` | — | `ServiceSummary[]` (`name`, `spanCount`, `errorCount`, `operations[]`) |
| GET | `/api/v1/services/{serviceName}/red` | `startUnixMs`, `endUnixMs`, `operation?`, `stepSeconds?` | `ServiceRedResponse`: `requestRatePerSec?`, `errorRatio?`, `durationP95Ms?`, `source`: `Metrics` \| `SpanApprox` |

**RED behavior:** PromQL first (OTel HTTP histogram metrics); on metrics failure → SpanApprox from inventory (ratio only; rate/p95 null).

### Traces — `Modules.Traces`

| Method | Route | Query / notes | Response |
|--------|-------|---------------|----------|
| GET | `/api/v1/traces` | service, operation, start/end, duration, limit, cursor | `Page<TraceSummary>` |
| GET | `/api/v1/traces/{traceId}` | 32-char hex | **Request view** `GetTraceResponse` (below) |
| GET | `/api/v1/errors` | `startUnixMs`, `endUnixMs`, `service?`, `limit?` | `ErrorGroupSummary[]` |

#### `GetTraceResponse` (request view)

| Field | Meaning |
|-------|---------|
| `trace` | `TraceDetail?` (null in logs-only mode) |
| `correlatedLogs` | `LogEntry[]` |
| `mode` | `Full` \| `SpansOnly` \| `LogsOnly` \| `Empty` |
| `markers` | log markers for waterfall (`spanId?`, `offsetMs`, level, …) |
| `erroredSpanCount` | spans with `status == Error` |
| `exceptions` | `{ exceptionType?, exceptionMessage?, spanId, service, operation }[]` |
| `dependencyGraph` | **P9** — nodes/edges for this trace (when landed) |

**404 rules:** only when **both** spans and logs empty. Log-only / spans-only → **200** + `mode`.

#### `ErrorGroupSummary`

`key`, `exceptionType?`, `message?`, `count`, `sampleTraceIds[]` (max 5).  
MVP: only traces with **root** `Status == Error`; detail fetch cap **20**.

### Logs — `Modules.Logs`

| Method | Route | Query | Response |
|--------|-------|-------|----------|
| GET | `/api/v1/logs` | `traceId` (required MVP), time range, limit | log page / list |

### OpenAPI / Scalar

- Doc: `GET /openapi/v1.json`
- UI: `GET /scalar/v1`

---

## 7. Domain model (Kernel, OTel-shaped)

| Type | Notes |
|------|--------|
| `Span` | id, parent, Service, Operation, StartTime (ms), DurationMs, Status, Tags, Events, **Kind**, **Resource** |
| `TraceDetail` / `TraceSummary` | root service/op, status, spans / counts |
| `LogEntry` | timestamp, level (`severity_number`), service, traceId?, spanId?, message, fields |
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
| **P9** | Per-trace dependency mini-map | — | 🔄 |
| post-core | Projects, rollup cache, global map | — | ⏸ |

---

## 9. Config / ops (short)

- TOML: `tessera.toml` + optional local; secrets `env:` / `file:`
- Victoria sections: `[victoria.traces|logs|metrics]`
- Auth: guest + admin bearer + LDAP/Keycloak (enabled in TOML)
- Storage: SQLite prefs via `IFileSystemLayout`
- Ports: host **1990**, Vite **1991**

---

## 10. How to update this doc

After a phase lands on `develop`:

1. Bump **Last updated** + tip SHA.
2. Refresh §3–§6 if routes/fields changed.
3. Tick §8 phase row + commit hashes.
4. Commit: `[.stbl](feat/docs): system-map — P9 deps` (example).

**Related:** `core-design.md` (semantics) · `modules.md` (older; prefer this map when drift) · ADRs.
