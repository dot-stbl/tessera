# Tessera

> APM UI for the Victoria stack — trace viewer with log correlation, like Kibana APM.

**Status:** pre-planning. No code yet. Architecture sketch lives in a `pi html_artifact`
(id: `victoria-apm-ui-architecture-sketch-mvp`).

## Intent

Self-hosted web app on top of `vtselect` / `vlselect` / `vmselect`.
Trace-centric: view traces → see logs for each trace → correlate by `trace_id`.

## Stack (proposed)

- **Backend** — .NET 10 ASP.NET Core minimal API, single-binary Linux deploy (AOT-friendly)
- **Frontend** — React 19 + Vite + shadcn/ui + Tailwind
- **Data** — VictoriaMetrics / VictoriaLogs / VictoriaTraces over HTTP (no ingest in MVP)

## MVP scope

Trace list (filterable) → trace detail (waterfall) → log panel per trace/span.
Settings (endpoints + bearer token), global time range, service filter.

Deferred: service map, RED metrics, flame graph, metric explorer, SLO, alerting.

## Ecosystem

Part of the `.stbl` ecosystem under `C:/Users/bradw/source/stbl/`.
Sibling projects: `plexor`, `anlytra`, `infrastructure.upva`, `kubix`, `pi-soly-framework`,
`pi-soly.framework`. Ecosystem-wide conventions TBD — to be documented by the owner.

## Conventions inherited from Plexor (reference project)

Until tessera-specific rules are written, follow the patterns from
[`../plexor/AGENTS.md`](../plexor/AGENTS.md):

- Two-name system: architecture theme names for schemas/modules, plain concept names
  for entities. `tessera` itself is the architecture theme root — internal schema/module
  names will follow `tile`, `mosaic`, `mortar`, `weave`, `capstone`, `pattern`,
  `fragment`, `veneer`, `join` (all in the same invented-word, single-token style).
- `Directory.Build.props`-style strict analyzers + `TreatWarningsAsErrors`.
- MinVer with `v`-prefix tags (`v0.1.0`).
- ghcr.io container publish per project.

## Next step

When the architecture sketch is validated, scaffold via `soly new tessera-mvp` and walk
through `discuss` / `plan` phases for task-by-task acceptance criteria.
