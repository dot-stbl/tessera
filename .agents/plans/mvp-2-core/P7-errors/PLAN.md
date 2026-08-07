# P7 — Errors (span status + exception events + inbox)

> Milestone: MVP-02 core. Status: planned 2026-08-07.  
> Locks: ADR-0002 §3, ADR-0003 (Errors HTTP → Traces).  
> Depends on: P5 (OTel Span/events), P6 (RequestView; CountErroredSpans exists).

## Goal

1. Pure **error derivation** in `Kernel/Analysis` (is-error, exception extract, group key).
2. Enrich **request view** with errored-span count + exception summaries.
3. **Errors inbox** HTTP under Traces: group recent error traces by `exception.type` + normalized message.

## Scope

### Kernel/Analysis (pure)
- `IsErrorSpan(Span)` — `Status == Error` (primary). Optional tag fallbacks already applied at map time for status; do not re-parse http codes unless status Unset and tags present (keep simple: Status == Error only if mapper already set status).
- `TryGetException(Span)` → type/message/stack from event name `"exception"` + `exception.*` attrs (semconv keys).
- `NormalizeErrorMessage(string)` — collapse numbers/UUIDs/hex for grouping stability (simple heuristics).
- `ErrorGroupKey(type, normalizedMessage)` — stable string for MVP grouping (fingerprint later).
- `CollectTraceErrors(TraceDetail)` → list of span-level error records + `ErroredSpanCount`.

### Traces module
- Enrich `GetTraceResponse` (or nested payload): `ErroredSpanCount`, `Exceptions` (type, message, spanId, service, operation).
- `GET /api/v1/errors` (ApiRoutes.Errors):
  - Query: startUnixMs, endUnixMs, service?, limit?
  - Orchestration: `SearchAsync` → take items with `Status == Error` (and optionally all if we want inner-error later — **MVP: root Error only** to bound cost) → for each, `GetByIdAsync` (cap e.g. 20 details) → Analysis group → `ErrorGroupSummary[]` (key, type, message, count, sampleTraceIds).
- `ErrorsService` + thin controller action (same TracesController or ErrorsController in Traces module — prefer separate controller file still in Traces project).
- Register DI in TracesModuleExtensions.

### Out
- Fingerprint hashing (post-MVP)
- Inner-error-only traces in inbox without root Error (P7.1)
- RED / Discovery
- FE

## Guardrails
- No new module; no Program.cs/Auth
- Kernel pure; ≤3 files/folder
- Cap detail fetches (document constant)

## Verify
```
dotnet format tessera.slnx --severity hidden
dotnet build tessera.slnx -c Debug
dotnet test tests/unit/core/Tessera.Shared.Unit
dotnet test tests/unit/modules/Tessera.Modules.Traces.Unit
dotnet test tests/unit/core/Tessera.ArchitectureTests
```
