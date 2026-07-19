# Tessera — Scope

> What ships in MVP, what's deferred, what's explicitly out.

## MVP scope

### In

Trace list, trace detail, log correlation per trace/span. Settings, time range,
service filter. Single-binary Linux deploy.

**Features:**

- **Trace list** — table with filters (service, status, min/max duration, time range)
- **Trace detail** — waterfall (parent/child spans + timing)
- **Log panel per trace** — auto-fetched by `trace_id`
- **Log panel per span** — filtered by `span_id` when present
- **Span detail side panel** — tags, events, duration
- **Global time range picker** + service dropdown
- **Settings page** — endpoint URLs + bearer token
- **Health check** — backend ↔ vt/vl/vm
- **Single Docker image, single-binary deploy**

**Tech stack:**

| Layer | Choice |
|-------|--------|
| Backend | .NET 10 ASP.NET Core controllers (matches Plexor), AOT-friendly single binary |
| Frontend | React 19 + Vite + shadcn/ui + Tailwind, bun + Turbo monorepo |
| Data | VictoriaMetrics / VictoriaLogs / VictoriaTraces over HTTP, **read-only** (no ingest) |
| Tests | xUnit + NSubstitute + Shouldly + Bogus + NetArchTest + Testcontainers |

**Quality gates:**

- `dotnet build tessera.slnx -c Debug` = 0 warnings, 0 errors
- `dotnet format tessera.slnx --severity hidden` = clean
- NetArchTest rule: max 5 .csproj per folder
- CI = same commands as local (one gate, identical)

### Out (deferred — possible future phases)

| Phase | Features |
|-------|----------|
| **Stretch 1** | Service map / dependency graph (auto-generated from `/select/jaeger/api/dependencies`) |
| **Stretch 2** | RED metrics panel per service (rate/errors/duration from VM via MetricsQL) |
| **Stretch 3** | Flame graph view per span |
| **Stretch 4** | Ad-hoc LogsQL query editor (beyond trace correlation) |
| **Stretch 5** | Ad-hoc MetricsQL query editor / metric explorer |

### Out of scope (not planned)

- **SLO budgets** — separate concern, defer indefinitely
- **Deployment markers** — needs CI integration, defer
- **Alerting** — many systems already do this (Alertmanager, Grafana alerts); defer
- **Anomaly detection / ML** — out of scope for proxy app
- **Multi-tenancy routing** — single tenant (`0`) only in MVP; multi-tenant is a routing concern
- **Auth beyond static bearer token** — OIDC / OAuth / SAML not planned
- **Saved views / pinning / sharing** — collaboration features, defer
- **User accounts / profiles** — single-tenant self-hosted, no user mgmt
- **Audit log of UI actions** — proxy is read-only, no UI actions to audit
- **Mobile-optimized UI** — desktop-first; mobile is "it works but not optimized"

## Phase model

Tessera uses 3-phase model:

| Phase | Goal | Definition of done |
|-------|------|-------------------|
| **MVP** | Trace viewer + log correlation, working end-to-end | `docker compose up` → user can browse traces → click → see logs |
| **Stretch** | Observability completeness (service map, RED, flame, query editor) | Same as MVP + each stretch feature has its own DoD in plan |
| **Out** | Explicitly excluded (multi-tenant, alerting, SLO, etc.) | No acceptance criteria — by design |

## MVP "Definition of done"

The MVP is done when ALL of these are true:

1. ✅ `docker run tessera` works (single binary, single port)
2. ✅ Settings page accepts endpoint URLs + token
3. ✅ Health endpoint returns per-Victoria status
4. ✅ Trace list loads and shows traces for any service in time range
5. ✅ Trace detail shows waterfall with correct parent/child relationships
6. ✅ Logs panel per trace shows logs with matching `trace_id`
7. ✅ All filters work (service, status, min/max duration, time range)
8. ✅ `dotnet build tessera.slnx` = 0 warnings, 0 errors
9. ✅ `dotnet test` passes for all unit tests
10. ✅ Integration test: Victoria stack via Testcontainers → full UI flow works
11. ✅ README with quickstart
12. ✅ Docker image published to `ghcr.io/<org>/tessera`

## Stretch phases — rough scope

### Stretch 1: Service map

- New module: `Tessera.Modules.ServiceMap`
- Endpoint: `/api/service-map`
- Backend: `vt /select/jaeger/api/dependencies` + aggregate
- UI: graph view (visx/cytoscape)

### Stretch 2: RED metrics

- New module: `Tessera.Modules.Metrics`
- Endpoint: `/api/metrics/red?service=...&time_range=...`
- Backend: `vm /prometheus/api/v1/query_range` with MetricsQL
- UI: per-service panel with rate/errors/duration

### Stretch 3: Flame graph

- Pure UI feature, no new backend
- Reuse `/api/traces/{id}` data
- Aggregate spans by name, render as icicle/flame

### Stretch 4: LogsQL query editor

- UI: code editor (Monaco) with LogsQL syntax highlighting
- Endpoint: `/api/logs?q=<LogsQL>` (already exists for trace correlation)

### Stretch 5: MetricsQL query editor

- UI: same as LogsQL but for metrics
- Endpoint: `/api/metrics/query?q=<MetricsQL>`

## What MVP explicitly does NOT solve

- **Causality analysis** — "why did this fail?" — needs ML or manual rules, out
- **Cost attribution** — "which service is expensive?" — needs billing integration, out
- **Capacity planning** — "when will we run out?" — needs forecasting, out
- **Synthetic monitoring** — out
- **Real user monitoring (RUM)** — out
- **Mobile APM** — out

## Related docs

- `architecture.md` — high-level architecture
- `modules.md` — per-module contracts (each module may have its own sub-scope)
- `../rules/coding/project-layers.md` — host/shared/modules structure
