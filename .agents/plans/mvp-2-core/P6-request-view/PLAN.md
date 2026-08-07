# P6 — Request view (correlation by `trace_id`)

> Milestone: MVP-02 core. Status: planned 2026-08-07.  
> Locks: ADR-0002 §2, ADR-0003 (A3 + Traces owns request view).  
> Depends on: P5 (OTel-shaped Span/LogEntry).

## Goal

Turn `GET /api/v1/traces/{traceId}` into a **degradable request view**:
union of spans + logs keyed by `trace_id`. Never 404 when either source
has data. Pure join/marker helpers live in `Kernel/Analysis/`; fetch
orchestration lives in `Modules.Traces`.

## Scope

### In
1. `Kernel/Analysis/` pure static:
   - `RequestViewMode` — `Full | SpansOnly | LogsOnly | Empty`
   - `RequestViewAssembly` (or similar) — classify mode from (trace?, logs)
   - `LogMarker` + builder: for each log with `span_id` matching a span,
     offset ms from span start (and/or trace start); logs without span_id
     → trace-level markers
   - `LogsBySpanIndex` — group logs by SpanId for side-panel filter
   - Optional light: `CountErroredSpans(trace)` for badge (P7 owns full errors)
2. Domain `RequestView` record in Kernel (or Traces Application) holding:
   `TraceDetail? Trace`, `IReadOnlyList<LogEntry> Logs`, `RequestViewMode Mode`,
   `IReadOnlyList<LogMarker> Markers`
3. `RequestViewService` in Traces module:
   - `Task.WhenAll` GetById + ListByTrace (need range even when trace missing —
     use generous window or query logs by trace_id without tight range when
     trace null; if ListByTrace requires range, use last-N hours default when
     no spans)
   - If both empty → ProviderNotFoundException (404)
   - Else return RequestView (never 404 for log-only)
4. Controller injects service only (not ITrace+ILog raw)
5. `GetTraceResponse` additive fields: `Mode` (string/enum), `Markers` optional
6. Unit tests: Analysis pure + service orchestration with fakes/NSubstitute

### Out
- P7 error grouping API
- P8 RED / IMetricsProvider consumption
- P9 dep mini-map
- FE / OpenAPI freeze
- Global service map / cache

## Guardrails
- Kernel Analysis pure (DomainPurityTests)
- ≤3 files/folder
- No Program.cs / Auth changes
- modules ↛ providers
- Controllers thin

## Verification
```
dotnet format tessera.slnx --severity hidden
dotnet build tessera.slnx -c Debug
dotnet test tests/unit/core/Tessera.Shared.Unit
dotnet test tests/unit/modules/Tessera.Modules.Traces.Unit
dotnet test tests/unit/core/Tessera.ArchitectureTests
```
