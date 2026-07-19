# Install

Deployment options for tessera: Docker (recommended), bare-metal systemd, or
Kubernetes. All paths assume single-binary deploy.

## Docker (recommended)

### Single container

```bash
docker run -d \
  --name tessera \
  --restart unless-stopped \
  -p 1990:1990 \
  -v /var/lib/tessera:/var/lib/tessera:rw \
  -v /etc/tessera:/etc/tessera:ro \
  -e TESSERA_ADMIN_TOKEN="$(openssl rand -hex 32)" \
  -e TESSERA_VICTORIA_TOKEN="your-victoria-bearer-token" \
  ghcr.io/<org>/tessera:latest
```

Port **1990** is the Tessera.Host backend port (see `coding/project-ports.md`). Override via `TESSERA__SERVER__PORT` env var.

**Volumes:**
- `/var/lib/tessera` — SQLite db + dashboards JSON (persistent)
- `/etc/tessera` — config (`tessera.toml` + optional `tessera.local.toml`, read-only mount is fine)

**Environment variables (all optional in MVP-01):**
- `TESSERA_ADMIN_TOKEN` — admin bearer token (optional in MVP-01; absent → admin endpoints reject 401, host starts cleanly)
- `TESSERA_VICTORIA_TOKEN` — Victoria stack bearer token (used by `Tessera.Providers.Victoria` Refit clients)
- `TESSERA__SERVER__PORT` — override default backend port (1990). ASP.NET Core convention: `__` (double underscore) = section nesting.

### Docker Compose (with Victoria stack)

`docker-compose.yml`:

```yaml
version: '3.8'

services:
  vt:
    image: victoriametrics/victoria-traces:v0.11.0
    command:
      - '--storageNode=-'
      - '--httpListenAddr=:10428'
    ports:
      - "10428:10428"
    volumes:
      - vt-data:/storage
    restart: unless-stopped

  vl:
    image: victoriametrics/victoria-logs:v1.5.0
    command:
      - '--storageDataPath=/storage'
      - '--httpListenAddr=:9428'
    ports:
      - "9428:9428"
    volumes:
      - vl-data:/storage
    restart: unless-stopped

  vm:
    image: victoriametrics/victoria-metrics:v1.95.0
    command:
      - '--storageDataPath=/storage'
      - '--httpListenAddr=:8429'
    ports:
      - "8429:8429"
    volumes:
      - vm-data:/storage
    restart: unless-stopped

  tessera:
    image: ghcr.io/<org>/tessera:latest
    depends_on:
      - vt
      - vl
      - vm
    ports:
      - "1990:1990"
    volumes:
      - tessera-data:/var/lib/tessera
      - ./tessera.toml:/etc/tessera/tessera.toml:ro
      - ./tessera.local.toml:/etc/tessera/tessera.local.toml:ro  # optional
    environment:
      TESSERA_ADMIN_TOKEN: "${TESSERA_ADMIN_TOKEN:-}"        # optional in MVP-01
      TESSERA_VICTORIA_TOKEN: "${TESSERA_VICTORIA_TOKEN:-}"
    restart: unless-stopped

volumes:
  vt-data:
  vl-data:
  vm-data:
  tessera-data:
```

`tessera.toml` (templates committed to `src/host/Tessera.Host/tessera.toml.example`):

```toml
version = "1"

[server]
host = "0.0.0.0"
port = 1990

[victoria]
tenant = "0"

[victoria.traces]
url = "http://vt:10428"
# token = "env:TESSERA_VICTORIA_TOKEN"   # resolved via SecretReference

[victoria.logs]
url = "http://vl:9428"
# token = "env:TESSERA_VICTORIA_TOKEN"

[victoria.metrics]
url = "http://vm:8429"
# token = "env:TESSERA_VICTORIA_TOKEN"

[storage]
data_dir = "/var/lib/tessera"

[telemetry]
service_name = "tessera"
log_level = "info"
```

`tessera.local.toml` (env-specific overrides; gitignored, NOT example):

```toml
# Deployment-specific overrides — never committed.
[victoria.traces]
token = "file:/run/secrets/tessera/victoria-token"

[victoria.logs]
token = "file:/run/secrets/tessera/victoria-token"
```

Secret prefix (`env:VAR` / `file:/path`) is parsed by
`Tessera.Shared.Kernel.Configuration.Paths.SecretReference.Resolve` at first
read — see `../architecture/config-format.md` §3.

**MVP-01 has no admin endpoints, so `TESSERA_ADMIN_TOKEN` is optional.** When
Phase 5+ adds admin endpoints, set the env var to a `openssl rand -hex 32`
token. The handler returns `NoResult()` when unset so host always starts.

## Bare-metal systemd

### Install binary

```bash
# Download
curl -L -o /usr/local/bin/tessera \
  https://github.com/<org>/tessera/releases/latest/download/tessera-linux-x64
chmod +x /usr/local/bin/tessera

# Verify
tessera --version
```

### Configuration

```bash
mkdir -p /etc/tessera
cp tessera.toml /etc/tessera/tessera.toml
chmod 644 /etc/tessera/tessera.toml

# Data directory
mkdir -p /var/lib/tessera
chown tessera:tessera /var/lib/tessera
```

### systemd unit

`/etc/systemd/system/tessera.service`:

```ini
[Unit]
Description=Tessera APM UI
Documentation=https://docs.tessera.example.com
After=network.target
Wants=network-online.target

[Service]
Type=notify
User=tessera
Group=tessera
ExecStart=/usr/local/bin/tessera
Restart=on-failure
RestartSec=5s
TimeoutStopSec=30s

# Environment — load token from sidecar file via TESSERA_*_FILE env,
# or directly inline (admin token optional in MVP-01).
EnvironmentFile=-/etc/tessera/tessera.env

# Sandboxing
ProtectSystem=strict
ProtectHome=true
PrivateTmp=true
PrivateDevices=true
NoNewPrivileges=true
ReadWritePaths=/var/lib/tessera
ConfigurationDirectory=tessera
StateDirectory=tessera

# Logging
StandardOutput=journal
StandardError=journal
SyslogIdentifier=tessera

[Install]
WantedBy=multi-user.target
```

`/etc/tessera/tessera.env`:

```bash
TESSERA_ADMIN_TOKEN=<64-char hex>
TESSERA_VICTORIA_TOKEN=<victoria-stack-token>
```

Configuration files:
- `/etc/tessera/tessera.toml` — base config (committed to git)
- `/etc/tessera/tessera.local.toml` — env-specific overrides (gitignored, optional)

### Enable and start

```bash
systemctl daemon-reload
systemctl enable tessera
systemctl start tessera
systemctl status tessera
journalctl -u tessera -f
```

## Kubernetes

Stretch — k8s manifests in `deploy/k8s/`:

```yaml
# deploy/k8s/deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: tessera
spec:
  replicas: 1
  selector:
    matchLabels:
      app: tessera
  template:
    metadata:
      labels:
        app: tessera
    spec:
      containers:
        - name: tessera
          image: ghcr.io/<org>/tessera:latest
          ports:
            - containerPort: 1990
          env:
            - name: TESSERA_ADMIN_TOKEN
              valueFrom:
                secretKeyRef:
                  name: tessera-secrets
                  key: admin-token
            - name: TESSERA_VICTORIA_TOKEN
              valueFrom:
                secretKeyRef:
                  name: tessera-secrets
                  key: victoria-token
          volumeMounts:
            - name: data
              mountPath: /var/lib/tessera
            - name: config
              mountPath: /etc/tessera/tessera.toml
              subPath: tessera.toml
              readOnly: true
          livenessProbe:
            httpGet:
              path: /health/live
              port: 1990
            initialDelaySeconds: 10
            periodSeconds: 30
          readinessProbe:
            httpGet:
              path: /health/ready
              port: 1990
            initialDelaySeconds: 5
            periodSeconds: 10
          resources:
            requests:
              cpu: 100m
              memory: 128Mi
            limits:
              cpu: 1000m
              memory: 512Mi
      volumes:
        - name: data
          persistentVolumeClaim:
            claimName: tessera-data
        - name: config
          configMap:
            name: tessera-config
```

## Backup

What to back up:
- `/var/lib/tessera/tessera.db` — SQLite
- `/var/lib/tessera/dashboards/` — JSON dashboards
- `/etc/tessera/tessera.toml` — config (already in git, but backup anyway)

```bash
#!/bin/bash
# /usr/local/bin/tessera-backup.sh
BACKUP_DIR=/var/backups/tessera
TIMESTAMP=$(date +%Y%m%d-%H%M%S)

mkdir -p "$BACKUP_DIR"
tar czf "$BACKUP_DIR/tessera-$TIMESTAMP.tgz" \
  -C /var/lib tessera \
  -C /etc tessera

# Retention: keep 7 days
find "$BACKUP_DIR" -name "tessera-*.tgz" -mtime +7 -delete
```

Schedule via systemd timer or cron:

```ini
# /etc/systemd/system/tessera-backup.timer
[Unit]
Description=Daily tessera backup

[Timer]
OnCalendar=daily
Persistent=true

[Install]
WantedBy=timers.target
```

## Upgrade

```bash
# 1. Stop service
systemctl stop tessera

# 2. Backup
tessera-backup.sh

# 3. Pull new version
docker pull ghcr.io/<org>/tessera:v0.2.0
# OR
curl -L -o /usr/local/bin/tessera.new ... && \
  mv /usr/local/bin/tessera.new /usr/local/bin/tessera && \
  chmod +x /usr/local/bin/tessera

# 4. Migrate config if needed (check CHANGELOG for breaking changes)

# 5. Start
systemctl start tessera

# 6. Verify
curl -sf http://localhost:1990/health/ready
```

**Schema migration:** SQLite db may need migration. Backend handles
`OnModelCreating` for EF Core; if breaking schema change, backup + manual
migration.

## Related docs

- `configure.md` — env vars, secrets, configuration patterns
- `troubleshoot.md` — common issues + debugging
- `storage.md` — what tessera stores on disk
- `../security/auth-model.md` — admin token
- `../architecture/config-format.md` — tessera.toml schema