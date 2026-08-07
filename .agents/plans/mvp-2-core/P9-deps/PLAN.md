# P9 — Per-trace dependency mini-map

> Milestone: MVP-02 core (last core phase).  
> Locks: ADR-0002 §5, ADR-0003 (per-trace map in core; Traces owns request-view).  
> Depends on: P5 SpanKind + Resource + tags.

## Goal

From one `TraceDetail`, derive a **dependency graph** (nodes + edges) for the
request-view mini-map. Pure rules in `Kernel/Analysis/Deps/`; expose on
`GetTraceResponse` (and/or `GET /api/v1/traces/{id}/dependencies`).

## Rules (from core-design §5)

1. CLIENT parent of SERVER with different `service.name` → edge client→server
2. CLIENT with `db.system` (or db.name) and no child SERVER → synthetic DB node + edge
3. CLIENT with `peer.service` / `server.address` and no child SERVER → synthetic external node + edge
4. Edge carries: call count, error count (spans with Status=Error on that edge)
5. PRODUCER/CONSUMER / links — **out of P9** (async later)

## Deliverables

### Kernel
- `DependencyNode` (Id, Name, Kind: Service|Database|External)
- `DependencyEdge` (FromId, ToId, CallCount, ErrorCount)
- `DependencyGraph` (Nodes, Edges)
- `DependencyAnalysis.FromTrace(TraceDetail)` pure static

### Traces
- Add `DependencyGraph?` to `GetTraceResponse` (null when logs-only / no spans)
- Populate in RequestViewService / mapping after trace loaded
- Optional thin route `ApiRoutes.TraceDependencies` if cleaner — prefer field on existing GET for fewer round-trips

### Tests
- Pure: CLIENT→SERVER, db.system synthetic, peer.service synthetic, error edge count
- Mapper/service: graph present when Full/SpansOnly; null/empty when LogsOnly

### Docs
- Update `.agents/docs/architecture/system-map.md` §6 GetTraceResponse + §8 P9 ✅

## Out
- Global service map / rollup cache
- Edge-level PromQL RED
- FE rendering

## Verify
dotnet format + build + Shared.Unit + Traces.Unit + ArchitectureTests
