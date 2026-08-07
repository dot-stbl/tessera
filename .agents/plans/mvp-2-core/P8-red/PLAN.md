# P8 — RED (Discovery + IMetricsProvider)

> Milestone: MVP-02 core. Status: planned 2026-08-07.  
> Locks: ADR-0002 §4, ADR-0003 (RED → Discovery).  
> Depends on: P5 (`IMetricsProvider` + Victoria metrics provider).

## Goal

Expose **Rate / Errors / Duration** for a service (and optionally operation)
via Discovery HTTP, preferring PromQL metrics with an honest span-approx
fallback when metrics backend is missing/fails.

## Scope

### Kernel/Analysis (pure, optional thin)
- `RedMetrics` record: `RequestRate` (per sec), `ErrorRatio` (0–1), `DurationP95Ms?`, `Source` enum `Metrics | SpanApprox`
- Helpers to extract a single scalar/series value from `MetricVector`/`MetricMatrix` (first sample) — pure

### Discovery module
- `GET /api/v1/services/{serviceName}/red?startUnixMs=&endUnixMs=&operation?&stepSeconds?`
- `RedService` / `ServiceRedService`:
  1. Try IMetricsProvider range/instant queries (PromQL templates for OTel HTTP metrics; document metric names used)
  2. On ProviderException / unconfigured → SpanApprox from `ServiceSummary` already known via IDiscoveryProvider (SpanCount/ErrorCount only — rate≈null or rough; **no p95**)
  3. Response includes `source` so UI can show "≈ from sampled traces"
- PromQL templates (configurable constants, not TOML yet):
  - rate: `sum(rate(http_server_request_duration_seconds_count{service_name="…"}[5m]))`
  - errors: 5xx share of same counter
  - p95: `histogram_quantile(0.95, sum by (le) (rate(http_server_request_duration_seconds_bucket{…}[5m])))`
- Escape service/operation for PromQL label values (basic quote/escape)
- Register DI; TimeProvider if default window needed — prefer required start/end

### Out
- Heavy span aggregation engine
- Per-operation RED on list endpoint (can add later)
- FE
- Modules.Metrics

## Guardrails
- No Program.cs / Auth
- Discovery only (not Traces)
- Cap: no new modules
- Kernel pure for Analysis helpers

## Verify
```
dotnet format tessera.slnx --severity hidden
dotnet build tessera.slnx -c Debug
dotnet test tests/unit/core/Tessera.Shared.Unit
dotnet test tests/unit/modules/Tessera.Modules.Discovery.Unit
dotnet test tests/unit/core/Tessera.ArchitectureTests
```
