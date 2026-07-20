# `compose/` — Tessera deployment + dev-stack workspace

All docker artefacts live in this directory. The repository root stays
clean of `Dockerfile`, `docker-compose.yml`, and `.dockerignore`.

## Layout

```
compose/
├── docker-compose.yml              # tessera + 3× Victoria + OTel collector + Grafana
├── Dockerfile                      # Tessera.Host runtime image
├── .dockerignore                   # build-context minimisation
├── otel-collector/
│   └── config.yaml                 # OTLP receiver + 3 export pipelines
├── grafana/
│   └── provisioning/
│       ├── datasources/victoria.yaml   # 3 datasources, trace→logs linked
│       └── dashboards/dashboards.yaml  # provider placeholder
├── config/
│   └── tessera.dev.toml            # in-container dev config
└── README.md                       # this file
```

## Bring up

```sh
docker compose -f compose/docker-compose.yml up -d
```

The stack blocks on Victoria healthchecks before Tessera starts, so a
plain `curl http://localhost:1990/api/v1/health` after `up -d` finishes
should immediately return `200 OK`.

## Tear down

```sh
docker compose -f compose/docker-compose.yml down -v
```

`-v` removes anonymous volumes; otherwise Victoria data persists across
runs.

## End-to-end verification

1. Health probe the host:
   ```sh
   curl -sf http://localhost:1990/api/v1/health | jq
   ```
2. Push synthetic telemetry via the load-gen:
   ```sh
   dotnet run --project tests/Tessera.LoadGen -- \
       --endpoint http://localhost:4317 \
       --services 5 \
       --rate 10 \
       --duration 30s \
       --scenario fanout
   ```
3. Discover the synthetic services through Tessera:
   ```sh
   curl -sf http://localhost:1990/api/v1/services | jq
   ```
4. Open Grafana — http://localhost:3000 (admin / admin) — and confirm
   the three Victoria datasources are present (VictoriaMetrics as
   default, VictoriaLogs, VictoriaTraces under Tempo type).

## Network topology

```
                                    ┌──────────────────┐
       ┌───────────────┐  OTLP/HTTP  │ OTel collector   │
       │ Tessera       │  (Tracing)  │ (otel/contrib)   │
       │ LoadGen       │ ──────────▶ │                  │
       │ (5–30 RPS)    │  4317/4318  └────────┬─────────┘
       └───────────────┘                      │
                                             │ fan-out per
                                             │ signal type
                          ┌──────────────────┼──────────────────┐
                          ▼                  ▼                  ▼
                  ┌───────────────┐  ┌───────────────┐  ┌───────────────┐
                  │ Victoria      │  │ Victoria      │  │ Victoria      │
                  │ Metrics       │  │ Logs          │  │ Traces        │
                  │ :8428         │  │ :9428         │  │ :10428        │
                  └───────▲───────┘  └───────▲───────┘  └───────▲───────┘
                          │                  │                  │
                          └──────────────────┴──────────────────┘
                                             │
                                             ▼
                                  ┌──────────────────┐
                                  │ Tessera.Host     │
                                  │ (consumer)       │
                                  │ :1990            │
                                  └──────────────────┘
```

Tessera is a thin proxy — the host reads from the three Victoria
endpoints over HTTP and exposes them under `/api/v1/*`; the host emits
no OTLP itself in MVP-01.
