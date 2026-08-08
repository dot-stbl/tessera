# Integration tests — Tessera house rules

> Status: wave-1 seed + host HTTP (2026-08-09). **Not** global Testcontainers/WAF dogma — house rules only.
>
> Stack runtime: **podman compose** (not docker). Compose file remains named
> `docker-compose.yml` for compose-spec compatibility.

## Layers

| Layer | Path | When |
|-------|------|------|
| Unit | `tests/unit/` | always (PR / local) |
| Integration | `tests/integration/` | podman stack + seed; CI self-hosted or opt-in local |
| Live | later / Trait | vironima; skip unless `TESSERA_IT_LIVE=1` |

## Integration contract

1. **Real Victoria** (containers via podman), not mocks of VT/VL.
2. **Fixed seeds** — known `trace_id`, service, logs (see `Seed/GoldenSeed.cs` + `GoldenSeeder`).
3. **HTTP against Tessera.Host** (process with free port + temp toml).
4. **Default soft-return** without `TESSERA_IT=1` so machines without podman stay green.
5. **No Respawn / no our telemetry DB** — only optional SQLite prefs for host.

## Wave 1

- [x] Layout + README + PLAN + compose VT+VL + csproj + golden constants + skippable smoke
- [x] Seed utility (OTLP HTTP insert; VL jsonline fallback) writing GoldenSeed
- [x] Host process with temp toml → container ports (free port discovery)
- [x] Scenarios: stack health, host health, traces search, get-trace Full/SpansOnly

## Wave 2

- VictoriaMetrics container + RED scenario
- Errors inbox with seeded error trace

## Wave 3

- Live suite against vironima (optional)

## Images (pinned later; draft)

| Service | Image (draft) | Port |
|---------|---------------|------|
| VictoriaTraces | `victoriametrics/victoria-traces:v0.4.0` | 10428 |
| VictoriaLogs | `victoriametrics/victoria-logs:v1.21.0` | 9428 |
| VictoriaMetrics | `victoriametrics/victoria-metrics:v1.110.0` | 8428 |

Confirm tags against what vironima runs before locking.

## Commands

```bash
# unit only
dotnet test tests/unit/...

# integration (opt-in) — podman, not docker
$env:TESSERA_IT=1
podman compose -f tests/integration/stack/docker-compose.yml up -d
dotnet test tests/integration/Tessera.Integration
podman compose -f tests/integration/stack/docker-compose.yml down
```
