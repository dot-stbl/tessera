# Tessera — Architecture

> APM UI for the Victoria stack. Trace viewer with log correlation, like Kibana APM.

**Status:** pre-implementation. Design captured here; code scaffold follows once
PLAN.md is approved.

## What is Tessera

Self-hosted web app that proxies the Victoria telemetry stack
(`vtselect` / `vlselect` / `vmselect`) into a Kibana-APM-like experience:

- **Trace list** — searchable, filterable (service, status, duration range, time)
- **Trace detail** — waterfall of parent/child spans with timing
- **Log correlation** — per-trace and per-span log panel via `trace_id` join
- **Settings** — endpoint URLs, bearer token, tenant (single-tenant MVP)

Stack:

| Layer | Tech |
|-------|------|
| Backend | **.NET 10** ASP.NET Core controllers (matches plexor), single-binary Linux deploy |
| Frontend | **React 19** + Vite + shadcn/ui + Tailwind, bun + Turbo monorepo |
| Data | **VictoriaMetrics / VictoriaLogs / VictoriaTraces** over HTTP (no ingest in MVP) |

## Module structure

Tessera uses Plexor's two-name system. Folder structure is `host/ shared/ modules/`
with a **hard 5-project-per-folder cap** (enforced via NetArchTest rule):

```
src/
├── host/              # 2 projects
│   ├── Tessera.Host/              ASP.NET Core controllers + DI + composition root
│   └── Tessera.Build.Tools/       MSBuild SDK + gates
├── shared/            # 5 projects (at cap)
│   ├── Tessera.Shared.Kernel/     Provider interfaces, Result<T>, Error, Id, TimeRange, Pagination, ProviderException types
│   ├── Tessera.Shared.Http/       Refit + resilience + OTel HTTP
│   ├── Tessera.Shared.Telemetry/  OTel setup + logging helpers
│   ├── Tessera.Shared.OpenApi/    Scalar + Swashbuckle annotations
│   └── Tessera.Shared.Validation/ FluentValidation helpers
└── modules/           # 4 projects (MVP)
    ├── Tessera.Modules.Traces/    TracesController + ITracesMapper + ITraceProvider consumer
    ├── Tessera.Modules.Logs/      LogsController + ILogProvider consumer
    ├── Tessera.Modules.Discovery/ DiscoveryController + IDiscoveryProvider consumer
    └── Tessera.Modules.Health/    HealthController + IHealthMapper + IHealthProvider consumer
```

**Theme words for internal naming:** `tessera`, `tile`, `mosaic`, `mortar`, `weave`,
`capstone`, `pattern`, `fragment`, `veneer`, `join` (see `coding/naming-tessera-theme.md`).

## Key flows

### "View trace → see logs" (core MVP)

```
1. User opens /traces. SPA calls GET /api/traces?service=...&start=...&end=...
2. Backend → vtselect /select/jaeger/api/traces
3. Returns TraceSummary[] → rendered as table

4. User clicks a row → SPA navigates to /traces/<trace_id>
5. SPA calls GET /api/traces/<trace_id>
6. Backend → vtselect /select/jaeger/api/traces/{trace_id}
7. Backend reconstructs span tree (parent/child from references[CHILD_OF])
8. Returns TraceDetail → rendered as waterfall

9. User opens Logs tab → SPA calls GET /api/logs?trace_id=<id>&start=...&end=...
10. Backend → vlselect /select/logsql/query?query=trace_id:"<id>"
11. Returns LogEntry[] (NDJSON) → rendered as log panel
```

### "Service inventory" (Discovery)

```
1. SPA calls GET /api/services?start=...&end=...
2. Backend → vtselect /select/jaeger/api/services
3. For each service, fetches operation list (parallel):
   → vtselect /select/jaeger/api/services/<svc>/operations
4. Aggregates into Service[] with operation counts
```

### "Health check" (per-Victoria)

```
1. SPA calls GET /api/health
2. Backend fans out to each Victoria endpoint:
   - vtselect /select/jaeger/api/services
   - vlselect /select/logsql/query?query=*&limit=1
   - vmselect /prometheus/api/v1/labels
3. Returns {vt: ok/error, vl: ok/error, vm: ok/error}
```

## Data flow

```
Browser (React SPA)
   │
   │ JSON (REST)
   ▼
ASP.NET Core host (Tessera.Host)
   │
   │ Refit HTTP clients
   ▼
┌──────────┐   ┌──────────┐   ┌──────────┐
│ vtselect │   │ vlselect │   │ vmselect │
│ (Jaeger) │   │ (LogsQL) │   │ (PromQL) │
└──────────┘   └──────────┘   └──────────┘
```

Backend is a thin proxy — no persistence, no business logic. All state is in
the user's browser (TanStack Query cache + zustand for filters).

## Architectural decisions

| Decision | Choice | Why |
|----------|--------|-----|
| API style | **Controllers** (`[ApiController]` + `ControllerBase`, matches Plexor) | Consistent with reference codebase, better tooling/testability. Minimal API was the prior choice; reversed 2026-07-19 — see STATE.md. |
| URL prefix | `ApiRoutes.Base = "api/v1"` (v1 baked in from MVP-01) | Single-line bump for v2; matches Plexor convention |
| HTTP client | **Refit** with interfaces | Type-safe, easy to mock in tests |
| Resilience | `Microsoft.Extensions.Http.Resilience` (Polly) | Standard ASP.NET pattern |
| Error pipeline | IExceptionHandler + ProblemDetails (RFC 9457) globally; per-endpoint try/catch banned | Plexor pattern; no `Result<T>` on HTTP boundary |
| JSON conventions | `JsonStringEnumConverter` for enum-as-string in wire format | Plexor pattern; OpenAPI emits lowercase status names |
| Time format on wire | **UTC unix ms** everywhere | Matches VT/VL/VM native format, no tz bugs |
| Tenant | **single, hardcoded `0`** in MVP | Multi-tenant is a routing concern, not MVP |
| Pagination | **cursor-based** on trace list | VT uses `limit`, not offset — cursor is forward-compatible |
| Span tree | **reconstructed on backend** | VT returns flat spans; building tree server-side is trivial & saves frontend work |
| Log volume cap | **500 per trace view**, no streaming yet | Good enough for MVP; add SSE/websocket later if needed |
| Auth | **bearer token in backend config**, SPA doesn't see it | Proxy pattern: SPA → backend → victoria |
| Service discovery | from **VT `/services`** | VT is source of truth for what we have traces for |
| State management | **zustand for filters, TanStack Query for server state** | Lightweight, no Redux ceremony |
| Frontend data viz | **visx** (or hand-rolled SVG) for waterfall | jaeger-style waterfall is ~300 LOC with visx primitives |

## Things explicitly out of MVP

See `scope.md` for full list. Highlights:

- Service map / dependency graph (stretch)
- RED metrics panel per service (stretch)
- Flame graph view (stretch)
- SLO budgets, deployment markers (out)
- Alerting, anomaly detection (out)
- Multi-tenancy routing (out)

## Open questions

These need answers before code starts (track in `.agents/STATE.md` once approved):

1. **Ingest pipeline** — how are traces getting into vt today?
   (otel-collector? vmagent? direct SDK?) — affects whether we need a sample collector.
2. **Auth** — single static token, or auth gateway in front of Victoria?
3. **Multi-tenant** — any chance MVP needs tenant routing, or is `0` forever?
4. **Deployment** — Docker only, or also bare-metal systemd / k8s manifests?
5. **Time range presets** — Grafana-style (15m/1h/6h/24h) or custom?
6. **Browser support** — latest evergreen only?
7. **Scale** — peak traces/sec, peak logs/sec, retention windows?
8. **PII compliance** — logs may contain PII; do we need redaction?

## Related docs

- `scope.md` — what's in / out for MVP
- `modules.md` — per-module contracts
- `api-contracts.md` — backend API shape (forthcoming)
- `victoria-stack.md` — Victoria API reference
- `ui/` — UI design docs (forthcoming)
