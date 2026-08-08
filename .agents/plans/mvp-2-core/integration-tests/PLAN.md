# Integration tests — Tessera house rules

> Status: scaffold 2026-08-08. **Not** global Testcontainers/WAF dogma — house rules only.

## Layers

| Layer | Path | When |
|-------|------|------|
| Unit | `tests/unit/` | always (PR / local) |
| Integration | `tests/integration/` | Docker + seed; CI self-hosted or opt-in local |
| Live | later / Trait | vironima; skip unless `TESSERA_IT_LIVE=1` |

## Integration contract

1. **Real Victoria** (containers), not mocks of VT/VL.
2. **Fixed seeds** — known `trace_id`, service, logs (see `seed/GoldenSeed.cs`).
3. **HTTP against Tessera.Host** (process or factory — wave 1 may probe stack only).
4. **Default skip** without `TESSERA_IT=1` so machines without Docker stay green.
5. **No Respawn / no our telemetry DB** — only optional SQLite prefs for host.

## Wave 1 (this scaffold + follow-up)

- [x] Layout + README + PLAN + compose VT+VL + csproj + golden constants + skippable smoke
- [ ] Seed utility (OTLP or insert API) writing GoldenSeed
- [ ] Host process or WAF with toml → container ports
- [ ] Scenarios: health, traces search, get-trace Full

## Wave 2

- VictoriaMetrics container + RED scenario
- Errors inbox with seeded error trace

## Wave 3

- Live suite against vironima (optional)

## Images (pinned later; draft)

| Service | Image (draft) | Port |
|---------|---------------|------|
| VictoriaTraces | `victoriametrics/victoria-traces:latest` | 10428 |
| VictoriaLogs | `victoriametrics/victoria-logs:latest` | 9428 |
| VictoriaMetrics | `victoriametrics/victoria-metrics:latest` | 8428 |

Confirm tags against what vironima runs before locking.

## Commands

```bash
# unit only
dotnet test tests/unit/...

# integration (opt-in)
$env:TESSERA_IT=1
docker compose -f tests/integration/stack/docker-compose.yml up -d
dotnet test tests/integration/Tessera.Integration
docker compose -f tests/integration/stack/docker-compose.yml down
```
