# Tessera state

## Milestone
MVP-01

## Status
planning

## Progress
0 phases, 0 plans, 0%

## Working agreement
- Owner confirms strategic decisions (architectural forks) before code work begins
- PLAN.md scaffolded via `soly_workflow new <slug>`; fleshed out via `discuss` then `plan`
- Each plan task has acceptance criteria — implemented exactly
- Decisions logged here for audit trail
- HANDOFF.md updated when strategic context shifts

## Decisions

| Date | Decision | Rationale |
|------|----------|-----------|
| 2026-07-19 | Tessera positioning: Grafana-analogue APM (multi-provider), NOT Victoria-specific UI | MVP-01 ships with Victoria as first provider via new `src/providers/` layer; future phases add Tempo/Jaeger/Loki/Mimir without refactoring modules. Architecture: provider interfaces in `Tessera.Shared.Kernel`, concrete implementations in new `src/providers/` top-level layer. No own collector/storage layer — reuse external collectors. |
| 2026-07-19 | New top-level `src/providers/` folder for concrete provider implementations | Mirrors Grafana datasource model: providers pluggable, modules provider-agnostic. Composition root wires active provider(s) in DI. |
| 2026-07-19 | Provider interfaces (`ITraceProvider`, `ILogProvider`, `IMetricsProvider`, `IDiscoveryProvider`, `IHealthProvider`) in `Tessera.Shared.Kernel` | Kernel already holds domain primitives; provider contracts are natural extension. Keeps `shared/` at 5-project cap. |
| 2026-07-19 | MVP-01 module scope: 4 modules — Traces + Logs + Discovery + Health | Matches HANDOFF baseline. Health as DI verification canary, Discovery aggregates services + streams. |
| 2026-07-19 | Victoria is MVP-01 first-and-only provider; second provider deferred to MVP-02+ | User explicitly chose "build abstraction now" over "defer to MVP-02". Cheap upfront, no refactor later. |
| 2026-07-19 | FE pipeline = plexor model (design-first OpenAPI + kubb codegen + MSW) | Adopted from plexor repo on owner hint. Hand-authored `contracts/tessera.openapi.yaml` is source of truth for MVP-01. `web/tooling/codegen/` workspace runs kubb 4.x (7 plugins: TS/Client/Zod/ReactQuery/Faker/MSW/custom-filter) to generate `web/apps/console/src/shared/api/src/{types,client,hooks,schemas,fixtures,msw,filters}`. FE consumes via React Query hooks. MSW intercepts when `VITE_USE_MOCKS=true`; real fetch when `false`. Hand-written `mock-data.ts` + `client.ts` deleted. When `Tessera.Host` emits real OpenAPI to `artifacts/openapi.json`, switch kubb input from contract to artifacts — screens stay untouched. |