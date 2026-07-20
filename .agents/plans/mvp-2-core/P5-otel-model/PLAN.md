# P5 — OTel domain model + Victoria→OTel mapping + IMetricsProvider

> Milestone: MVP-02 core (track 2). Status: planned 2026-07-21.
> Locks: `.agents/docs/decisions/0002-core-observability-model.md`, `.agents/docs/core-design.md`.
> Track position: **P5 is the data foundation** — it produces the OTel-shaped inputs that
> P6 (correlation), P7 (errors), P8 (RED), P9 (dependencies) consume. It builds **model +
> mapping only** — no derivation, no metrics module, no contract/FE changes.

## Goal

Make the existing Kernel domain properly **OTel-semconv-shaped** (span kind, resource
attributes, aligned status, severity, exception events), fix three real mapping bugs, and add
`IMetricsProvider` (Kernel) + a `VictoriaMetricsProvider` (thin PromQL Refit client + mapper).
Providers map `wire → OTel`; the smart logic stays above them (P6–P9).

## Locked decisions (from the design session)

- **Only OpenTelemetry** for logs/traces; domain = OTel semconv (ADR-0002 D1).
- Provider = transport + `wire→OTel` mapping; derivation lives above (ADR-0002 D2).
- **span.kind + status = first-class fields**; attribute keys via **vendored** semconv constants
  (NOT the `OpenTelemetry.SemanticConventions` package — it has stability concerns, and Kernel is
  a minimal leaf; vendor ~15 keys mirroring semconv 1.43.0).
- **Resource denormalized on Span/LogEntry, interned per process** during mapping (one instance
  per distinct process, shared by reference). Small `Resource` record (`ServiceName`,
  `ServiceNamespace?`, `DeploymentEnvironment?`) + open `Attributes` dict (for project predicates).
- Log level from **`severity_number`** (text fallback).
- Hand-written `internal static` mappers for `wire→domain`; Mapperly reserved for `domain→DTO`.
- **Derivation principle (track-wide):** pure derivation (tree/deps/errors/RED-assembly) →
  `Kernel/Analysis/` pure static (passes `DomainPurityTests`, avoids a new shared project that
  would break the 5-project cap); orchestration (fetch N providers + combine) → in the module.
  *(P5 only reserves this; population is P6–P9.)*
- P5 = interface (Kernel) + provider (Victoria) only. **No `Tessera.Modules.Metrics` yet**
  (would be the 5th module — comes with RED/dashboards, P8).

## Guardrails

- **Do NOT touch** `src/host/Tessera.Host/Program.cs`, `Tessera.Shared.Authentication/**`, or any
  auth/config wiring — the background auth agent owns those. All metrics DI goes inside
  `AddVictoriaProvider` (`VictoriaServicesRegistration`), not `Program.cs`.
- Kernel stays pure (`DomainPurityTests`: no ASP.NET / EF / HttpClient / Refit).
- ≤3 `.cs` per folder → nest (`Implementation/Metrics/`, `Dto/Prometheus/`). No new projects
  (5-project cap; shared is at 5) — everything lands in existing Kernel + Victoria.
- No module contract (`GetTraceResponse` etc.) or FE `types.ts` changes — those grow in P6/P8.
  To keep the module mapper compiling, **keep `Span.Service`** as a convenience alias of
  `Resource.ServiceName` (dedupe later).

## Tasks

### T1 — Kernel: SpanKind + Resource + vendored semconv constants
- `Domain/Spans/SpanKind.cs` — enum `Unspecified/Internal/Server/Client/Producer/Consumer` (OTLP values).
- `Domain/Resources/Resource.cs` — `record Resource(string ServiceName, string? ServiceNamespace, string? DeploymentEnvironment, IReadOnlyDictionary<string,string> Attributes)`.
- `Observability/SemanticConventions.cs` — vendored key constants (`service.name`, `service.namespace`, `deployment.environment`, `span.kind`, `otel.status_code`, `http.response.status_code`, `rpc.grpc.status_code`, `exception.type/message/stacktrace`, `db.system`, `peer.service`, `server.address`, `messaging.system`, `messaging.destination.name`). Header comment: "mirrors OTel semconv 1.43.0".
- Extend `Span`: add `SpanKind Kind` + `Resource Resource` (keep `Service`).
- **Acceptance:** Kernel builds 0/0; `DomainPurityTests` green; all public types immutable records with XML docs; zero new NuGet deps.

### T2 — Kernel: metrics domain + IMetricsProvider
- `Domain/Metrics/` — `MetricSample(long TimestampUnixMs, double Value)`, `MetricSeries(IReadOnlyDictionary<string,string> Labels, IReadOnlyList<MetricSample> Samples)`, `MetricMatrix(IReadOnlyList<MetricSeries> Series)`, `MetricVector(IReadOnlyList<MetricSeries> Series)` (single-sample series), `MetricScalar(MetricSample Sample)`.
- `Providers/Metrics/IMetricsProvider.cs` — `QueryRangeAsync(MetricsRangeQuery, ct)`, `QueryInstantAsync(MetricsInstantQuery, ct)`, `LabelValuesAsync(string label, TimeRange, ct)`.
- `Providers/Metrics/MetricsRangeQuery.cs` (`string Query, long StartUnixMs, long EndUnixMs, int StepSeconds`) + `MetricsInstantQuery.cs` (`string Query, long TimeUnixMs`).
- **Acceptance:** interface pure (no HttpClient/Refit); Kernel builds; purity tests green.

### T3 — Victoria trace mapper: OTel-ify (`VictoriaTraceMapper`)
- Promote `span.kind` tag → `SpanKind` (verify VT's actual key/values).
- Map `JaegerProcess.Tags` → `Resource` (ServiceName from process; namespace/env from tags via semconv keys; rest → `Attributes`); **intern one `Resource` per `ProcessID`** within the trace.
- `ToStatus`: `otel.status_code`/`error` first → then fallback `http.response.status_code`≥500 / `rpc.grpc.status_code`≠0.
- **Fix `StartTime` µs→ms** on `Span`/`TraceDetail`/`TraceSummary` (`dto.StartTime / 1000`).
- Explicit `exception` event: name `"exception"` + carry `exception.*` attributes.
- **Acceptance:** `Tessera.Providers.Victoria.Unit` covers kind, resource+interning, status fallback, µs→ms, exception event; build 0/0.

### T4 — Victoria log mapper: OTel-ify + fix bugs (`VictoriaLogMapper`)
- Level from `severity_number` (1–24 → `LogLevel`) with `level`/`severity_text` fallback.
- `LogEntry.Service` from resource `service.name` (not `_stream`).
- **Unify `trace_id` vs `_trace_id`** — `BuildLogsQuery` filters on `_trace_id` but `ParseNdjson` reads `trace_id`; make both use the verified field. *(Verify on live VL — a wrong query field = silent empty correlation.)*
- **Acceptance:** log mapper tests updated; query/parse field unified; verify item noted in PR.

### T5 — Victoria metrics client + provider + mapper
- `Clients/IVictoriaMetricsClient.cs` (Refit): `/prometheus/api/v1/query`, `/query_range`, `/label/{name}/values` (tenant-prefixed like traces/logs). Registered in `VictoriaServicesRegistration.RegisterClients` via `AddTesseraRefitClient` using `options.Metrics?.Url` — **skip when null**.
- `Dto/Prometheus/` — `PromQueryResponse(status, data{resultType, result})` + matrix/vector/scalar result records.
- `Implementation/Metrics/VictoriaMetricsMapper.cs` (`internal static`): Prom JSON → `MetricMatrix`/`MetricVector`/`MetricScalar`; unix-seconds→ms; string-encoded values parsed.
- `Implementation/Metrics/VictoriaMetricsProvider.cs : IMetricsProvider`. When `Metrics.Url` is null, surface a typed `ProviderException` ("metrics backend not configured") so downstream RED can degrade gracefully.
- Register provider + `IMetricsProvider` binding in `RegisterProviders`.
- **Acceptance:** metrics mapper unit tests; DI resolves `IMetricsProvider`; **`Program.cs` untouched**; build 0/0.

## Out of scope (later plans)
Derivation logic (tree/deps/errors/RED assembly) — P6–P9. `Tessera.Modules.Metrics` + controller.
Module contract + FE `types.ts` changes. Span `links[]`. Typed attributes. Global service map.

## Verify flags (resolve during execution)
1. `trace_id` vs `_trace_id` — confirm the real VL field name against a live VictoriaLogs.
2. `StartTime` µs→ms — audit the module→DTO mapper (`TracesMapper`) so the fix isn't double-applied.
3. `span.kind` tag key/values as emitted by VictoriaTraces (Jaeger vs OTLP naming).
4. semconv keys — vendored file, not a package dependency.

## Verification
```
dotnet format tessera.slnx --severity hidden
dotnet build tessera.slnx -c Debug            # exit 0, 0 warnings
dotnet test tests/unit/core/Tessera.Shared.Unit tests/unit/providers/Tessera.Providers.Victoria.Unit
```
Architecture tests (`DomainPurityTests`, `LayerIsolationTests`, `FolderStructureTests`) must stay green.
