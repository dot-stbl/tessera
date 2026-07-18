---
description: tessera port pool 1990–2120 for the app + dev tooling; reserved ranges for backend, frontend, services, external dev tools
globs: ["**/*.csproj", "**/appsettings*.json", "**/tessera.toml", "**/vite.config.ts", "**/Dockerfile", "**/docker-compose*.yml"]
always: true
---

# Port pool

Tessera reserves port range **1990–2120** for the application and related dev
tooling. Victoria components (VT/VL/VM) use their standard ports (10428, 9428,
8429) — out of scope.

## Why a custom range

Standard ports collide with other tooling:
- 3000 (Node/React dev), 5173 (vite), 8080 (HTTP alt), 9090 (Prometheus),
  5432 (Postgres), 6379 (Redis), 27017 (Mongo), etc.

Picking a dedicated range prevents surprise collisions on dev machines and
makes port allocation explicit + reviewable.

## Reserved ranges

| Range | Purpose | Notes |
|-------|---------|-------|
| **1990** | **Tessera.Host HTTP API (backend)** | primary port, env `TESSERA_PORT` |
| **1991** | **Vite dev server (frontend)** | `vite.config.ts` `server.port` |
| 1992 | Tessera `/metrics` Prometheus endpoint (separate port) | when not served on main port |
| 1993–1999 | Reserved (future internal services) | OTel collector sidecar, admin port, etc. |
| 2000–2020 | Reserved (external dev tooling) | Jaeger UI, Grafana, Loki, etc. for local dev |
| 2021–2120 | Reserved (future expansion) | capacity for growth |

## Backend default

`Tessera.Host` listens on **1990** by default (`[server] port = 1990` in `tessera.toml`).

```toml
[server]
host = "0.0.0.0"
port = 1990
```

Override via env var (ASP.NET convention):

```bash
ASPNETCORE_URLS=http://0.0.0.0:8080 tessera
```

Or our convention:

```bash
TESSERA_SERVER__PORT=8080 tessera   # double underscore for nested sections
```

## Frontend default

`vite.config.ts` uses port **1991** for the dev server:

```ts
server: {
  port: 1991,
  strictPort: true,
}
```

`strictPort: true` means vite **fails** to start if 1991 is taken (no silent fallback to 5173).

## Container port mapping

Docker compose / docker run:

```bash
docker run -p 1990:1990 ghcr.io/<org>/tessera
```

```yaml
services:
  tessera:
    ports:
      - "1990:1990"
```

Host and container ports both in our pool — no external mappings needed.

## Test ports

WebApplicationFactory / integration tests use **port 0** (OS-assigned):

```csharp
WebApplicationFactory<Program>.WithWebHostBuilder(b => b.UseSetting("server.urls", "http://127.0.0.1:0"));
```

Testcontainers expose Victoria on dynamic ports — no allocation needed.

## Anti-patterns

```bash
❌ Port 8080 (commonly used, conflicts with HTTP alt / Tomcat / Jenkins)
❌ Port 3000 (React/Node default, conflicts)
❌ Port 5173 (vite default — we override to 1991)
❌ Port 9090 (Prometheus default — we use 1992 for /metrics on separate port, OR
              keep 9090 if Prometheus is a separate process)
❌ Random OS-assigned port in production (must be explicit)
❌ Port outside 1990–2120 range (use this rule or update it via STATE.md decision)
```

## When to extend

If we need more than 130 internal ports (extremely unlikely — typical SaaS uses
5–10 ports total), propose new range in `.agents/STATE.md` Decisions and
update this rule.

## Related

- `.agents/docs/operations/configure.md` — env vars, port references
- `.agents/docs/operations/install.md` — docker port mappings
- `coding/project-naming-and-setup.md` — overall project setup
- `process/build-verification.md` — build gate catches `port:` outside range (stretch)