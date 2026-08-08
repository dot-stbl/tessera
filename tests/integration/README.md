# Tessera integration tests

House rules (not global testing-integration.md):

1. **Unit** = cheap, always. **Integration** = podman stack + fixed seeds + HTTP.
2. **Live lab** (vironima) is optional and separate — not the PR gate.
3. Integration is **opt-in**: set `TESSERA_IT=1` or tests soft-return (exit 0).
4. Prefer **known fixtures** over “whatever is in the cluster”.
5. Stack definition lives in `stack/docker-compose.yml` — bring up with **podman compose** (not docker).

## Layout

```
tests/integration/
  README.md                 ← this file
  Tessera.Integration/      ← xUnit project
  stack/docker-compose.yml  ← VT + VL (+ VM later)
  Seed/                     ← GoldenSeed + GoldenSeeder
  Support/                  ← gate, endpoints, host process
  Scenarios/                ← stack smoke + host HTTP
```

## Run (podman)

```powershell
$env:TESSERA_IT = "1"
podman compose -f tests/integration/stack/docker-compose.yml up -d
# wait until health is green:
#   curl http://127.0.0.1:10428/health
#   curl http://127.0.0.1:9428/health
dotnet test tests/integration/Tessera.Integration/Tessera.Integration.csproj
podman compose -f tests/integration/stack/docker-compose.yml down
```

Without `TESSERA_IT=1`, the project still builds and tests **soft-return** (exit 0).

Optional URL overrides:

| Env | Default |
|-----|---------|
| `TESSERA_IT_TRACES_URL` | `http://127.0.0.1:10428` |
| `TESSERA_IT_LOGS_URL` | `http://127.0.0.1:9428` |
| `TESSERA_IT_METRICS_URL` | `http://127.0.0.1:8428` |

## Seed paths (wave 1)

`Seed/GoldenSeeder` writes fixed `GoldenSeed` identity **without** an OTEL collector:

| Backend | Primary insert | Fallback |
|---------|----------------|----------|
| VictoriaTraces | `POST {traces}/insert/opentelemetry/v1/traces` (OTLP/HTTP JSON) | — |
| VictoriaLogs | `POST {logs}/insert/opentelemetry/v1/logs` | `POST {logs}/insert/jsonline` with `_trace_id` / `_span_id` |

OTLP JSON uses **base64** byte fields for `traceId` / `spanId` (protobuf JSON mapping). VL jsonline uses hex string fields matching Tessera’s LogsQL correlation (`_trace_id`).

If a path returns non-success, the seeder surfaces the status/body so you can align image versions.

## Host under test

`Support/TesseraHostProcess` starts `Tessera.Host` with a temp `tessera.toml` pointed at the stack URLs, `TESSERA_CONFIG` set to that file, and a **free loopback port** (avoids clashing with a local 1990). Scenarios wait for `GET /api/v1/health` → 200.

## Wave status

See `.agents/plans/mvp-2-core/integration-tests/PLAN.md`.
