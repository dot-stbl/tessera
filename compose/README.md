# `compose/` — Tessera deployment + dev-stack workspace

All docker artefacts live in this directory. The repository root stays
clean of `Dockerfile`, `docker-compose.yml`, and `.dockerignore`.

## Layout

```
compose/
├── docker-compose.yml              # tessera + 3× Victoria + OTel collector
├── Dockerfile                      # Tessera.Host runtime image
├── .dockerignore                   # build-context minimisation
├── otel-collector/
│   └── config.yaml                 # OTLP receiver + 3 export pipelines
├── config/
│   └── tessera.conf/
│       └── tessera.toml            # in-container config
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

`-v` removes anonymous volumes; otherwise Victoria data and the
Tessera data tree persist across runs.

## Storage layout — file-based

Tessera persists its data on the local filesystem — there is no
external database. State lives under `~/etc/tessera/...` on the host
(mounted at `/var/lib/tessera` inside the container):

```
~/etc/tessera/                                  # TESSERA_DATA_ROOT
├── config/
│   ├── tessera.toml                 # main config (TOML)
│   ├── tessera.local.toml           # host-specific overrides (optional)
│   └── users/                       # one file per principal
│       ├── root.toml                # user-level scoped config
│       └── ...
├── state/
│   ├── sessions/                   # transient session state
│   │   └── <session-id>.json
│   └── cache/                       # derived state, regenerable
│       └── ...
├── tenants/                         # multi-tenant data root
│   └── 0/                          # single-tenant default ("0")
│       └── ...
└── logs/                            # runtime logs (if not stderr-only)
```

The directory mirrors the Linux-user style convention: principals
own subtree, the boundary is the filesystem. Each user file is a
plain TOML and can be inspected with `cat`, edited with `$EDITOR`,
diffed against another user's file.

Boot-time layout is created if missing — no manual `mkdir -p` needed
on a fresh checkout.

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
4. Inspect the persisted data tree from the host:
   ```sh
   docker compose -f compose/docker-compose.yml exec tessera \
       ls /var/lib/tessera/tenants/0/
   ```

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
                          ┌───────────────────┼──────────────────┐
                          ▼                   ▼                  ▼
                  ┌───────────────┐   ┌───────────────┐  ┌───────────────┐
                  │ Victoria      │   │ Victoria      │  │ Victoria      │
                  │ Metrics       │   │ Logs          │  │ Traces        │
                  │ :8428         │   │ :9428         │  │ :10428        │
                  └───────▲───────┘   └───────▲───────┘  └───────▲───────┘
                          │                   │                  │
                          └───────────────────┴──────────────────┘
                                              │
                                              ▼
                                   ┌──────────────────┐
                                   │ Tessera.Host     │
                                   │ (consumer)       │
                                   │ :1990            │
                                   └──────────────────┘
```

Tessera is a thin proxy — the host reads from the three Victoria
endpoints over HTTP and exposes them under `/api/v1/*`.
