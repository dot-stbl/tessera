# Configure

Reference for tessera configuration: environment variables, secrets, common
patterns.

## Configuration sources (priority order)

Tessera loads configuration from multiple sources, **later wins**:

1. `tessera.toml` — main config file (see `../architecture/config-format.md`)
2. `tessera.local.toml` — local override (gitignored)
3. **Environment variables** — `TESSERA_*` prefixed
4. **Environment file refs** — `env:VAR_NAME` and `file:/path` in TOML

## Path resolution

Config file search order:

```
1. $TESSERA_CONFIG env var
2. /etc/tessera/tessera.toml
3. $XDG_CONFIG_HOME/tessera/tessera.toml (default ~/.config/tessera/)
4. ./tessera.toml
```

## Environment variables

### Server

| Variable | Default | Description |
|----------|---------|-------------|
| `TESSERA_CONFIG` | (auto) | Path to tessera.toml |
| `ASPNETCORE_URLS` | `http://0.0.0.0:1990` | Listen address (overrides `[server]` section; port 1990 per coding/project-ports.md) |
| `ASPNETCORE_ENVIRONMENT` | `Production` | `Development` enables verbose logs |

### Authentication

| Variable | Required | Description |
|----------|----------|-------------|
| `TESSERA_ADMIN_TOKEN` | **optional in MVP-01** — unset → admin scheme disabled (handler returns `NoResult`) | Bearer token for **future** admin endpoints (none exist in MVP-01) |
| `TESSERA__ADMIN__TOKEN` | alternative | ASP.NET Core double-underscore convention (`[auth]:admin:token` config section); if both set, `configuration["TESSERA_ADMIN_TOKEN"]` wins (see `AddTesseraAdminAuthentication` in `Tessera.Shared.Authentication.AuthenticationInstallerExtensions`) |

**Generate strong token (Phase 5+ admin endpoints):**

```bash
openssl rand -hex 32  # 64-char hex
# Store in vault, secret manager, or env file with 0600 perms
```

### Victoria stack

| Variable | Required | Description |
|----------|----------|-------------|
| `TESSERA_VICTORIA_TOKEN` | no (mandatory for any non-anonymous VT/VL/VM) | Bearer token applied uniformly to vtselect / vlselect / vmselect via `[victoria.*]` `token = "env:TESSERA_VICTORIA_TOKEN"` inline reference |
| `TESSERA_VICTORIA_TOKEN_FILE` | alternative | Path to file containing the token (resolved by inline `file:/path` prefix in TOML) |

### Observability

| Variable | Default | Description |
|----------|---------|-------------|
| `OTEL_EXPORTER_OTLP_ENDPOINT` | (none) | OTLP gRPC endpoint for trace export |
| `OTEL_EXPORTER_OTLP_HEADERS` | (none) | OTLP headers (auth, etc.) |
| `OTEL_SERVICE_NAME` | `tessera` | Override service name |

### Storage

| Variable | Default | Description |
|----------|---------|-------------|
| `TESSERA_STORAGE__DATA_DIR` | `/var/lib/tessera` | Override `data_dir` from TOML |

Note: double underscore `__` for nested sections (standard .NET convention).

## Secrets management

### Never in TOML files

TOML files are version-controlled. Secrets leak through git history.

```toml
# ❌ WRONG — secret in TOML
[victoria.traces]
url = "http://vt:10428"
token = "abc123-secret"

# ✅ CORRECT — env var reference
[victoria.traces]
url = "http://vt:10428"
token = "env:TESSERA_VICTORIA_TOKEN"
```

### Options for secrets

| Method | When |
|--------|------|
| Env var directly | Simplest. Good for Docker, K8s. |
| Env var file ref (`file:/path`) | When env vars are too long or dynamic. Good for systemd. |
| Docker secrets | Swarm / Compose secret mounts. |
| Kubernetes secrets | K8s secret + env var injection. |
| HashiCorp Vault | Dynamic secrets, rotation. |
| File with 0600 perms | Last resort. Document location in deploy runbook. |

### Docker secrets example

```yaml
services:
  tessera:
    image: ghcr.io/<org>/tessera:latest
    secrets:
      - tessera_admin_token
      - tessera_victoria_token
    environment:
      TESSERA_ADMIN_TOKEN_FILE: /run/secrets/tessera_admin_token
      TESSERA_VICTORIA_TOKEN_FILE: /run/secrets/tessera_victoria_token

secrets:
  tessera_admin_token:
    file: ./secrets/admin-token.txt
  tessera_victoria_token:
    file: ./secrets/victoria-token.txt
```

`secrets/admin-token.txt`:

```
abc123-def456-789012-...
```

### Kubernetes secrets example

```yaml
apiVersion: v1
kind: Secret
metadata:
  name: tessera-secrets
type: Opaque
stringData:
  admin-token: abc123-def456-789012-...
  victoria-token: xyz789-uvw456-...
```

```yaml
# In deployment:
env:
  - name: TESSERA_ADMIN_TOKEN
    valueFrom:
      secretKeyRef:
        name: tessera-secrets
        key: admin-token
```

## Common configuration patterns

### Single Victoria stack (dev)

```toml
version = "1"

[victoria]
tenant = "0"

[victoria.traces]
url = "http://localhost:10428"

[victoria.logs]
url = "http://localhost:9428"

[victoria.metrics]
url = "http://localhost:8429"
```

### Production with TLS

```toml
[victoria.traces]
url = "https://vt.internal.example.com:10428"

[victoria.logs]
url = "https://vl.internal.example.com:9428"
```

### Multiple dashboards dirs

```toml
[storage]
data_dir = "/var/lib/tessera"
dashboards_dir = "/etc/tessera/dashboards"   # mount shared dashboards here
```

(Stretch feature — MVP uses `data_dir/dashboards/`.)

### Disable features

```toml
[telemetry]
metrics_endpoint = false          # /metrics disabled

[auth]
guest_enabled = false             # require auth for all endpoints (stretch only)

[cache]
enabled = false                   # always fetch from Victoria
```

## Configuration validation

`ValidateOnStart()` ensures invalid config crashes the process at startup:

```
$ tessera
[ERR] Validation failed for option VictoriaOptions:
       - victoria.traces.url is required
       - victoria.timeout_ms must be >= 100
Process exit code: 1
```

Don't disable validation. Fix the config.

## Hot reload — NOT supported (MVP)

Tessera MVP does **not** reload config on file change. Restart required:

```bash
systemctl restart tessera
```

Stretch: `IOptionsMonitor<T>` based hot reload (see roadmap).

## Environment-specific overrides

`/etc/tessera/tessera.local.toml`:

```toml
[victoria.traces]
url = "http://vt-prod.internal:10428"   # override main config URL

[storage]
data_dir = "/var/lib/tessera-prod"      # override data dir
```

Loaded **after** main config; later values win. Per-host customization without
modifying version-controlled files.

## Anti-patterns

```bash
# ❌ Putting secrets in env file checked into git
echo "TESSERA_ADMIN_TOKEN=abc123" >> .env  # .env is committed

# ✅ Use .env.example for documentation, .env in .gitignore
# .env (gitignored)
TESSERA_ADMIN_TOKEN=abc123
TESSERA_VICTORIA_TOKEN=xyz789
```

```toml
# ❌ Hardcoded URLs
[victoria.traces]
url = "http://192.168.1.100:10428"   # ← IP will change
```

```toml
# ❌ Multiple sources of truth
[victoria.traces]
url = "http://vt:10428"             # and ALSO env var TESSERA_VICTORIA__TRACES__URL=http://other
```

## Related docs

- `../architecture/config-format.md` — TOML schema
- `install.md` — deployment setup
- `troubleshoot.md` — debug config issues
- `../security/auth-model.md` — admin token
- `../architecture/multi-tenancy.md` — tenant config