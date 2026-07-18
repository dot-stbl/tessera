# Troubleshoot

Common tessera issues and debugging recipes.

## Tessera won't start

### Symptom: `Validation failed for option VictoriaOptions`

```
[ERR] Validation failed for option VictoriaOptions:
       - victoria.traces.url is required
```

**Cause:** required config missing.

**Fix:** add missing field to `tessera.toml` (or set env var):

```toml
[victoria.traces]
url = "http://vt:10428"     # ← required
```

### Symptom: `TOML parse error at line N`

```
[ERR] Failed to parse tessera.toml: TOML parse error at line 5 column 3
```

**Fix:** validate syntax. Common errors:

```toml
# ❌ Missing quotes around string
title = My Dashboard

# ✅ Correct
title = "My Dashboard"

# ❌ Mixed case in section name
[Victoria.traces]

# ✅ Correct (TOML is case-sensitive, matches C# binder)
[victoria.traces]
```

### Symptom: `Port 1990 is already in use`

```
[ERR] Failed to bind to address http://0.0.0.0:1990: address already in use
```

**Fix:** change port in `[server]` or kill conflicting process.

```toml
[server]
host = "0.0.0.0"
port = 1991
```

Or set `ASPNETCORE_URLS=http://0.0.0.0:9000` to override.

## Tessera starts but health check fails

### Symptom: `/health/ready` returns 503

```
$ curl -i http://localhost:1990/health/ready
HTTP/1.1 503 Service Unavailable
```

**Cause:** Victoria backends unreachable.

**Debug:**

```bash
# 1. Check Victoria is reachable from tessera host
curl -sf http://vt:10428/select/0/jaeger/api/services

# 2. Check tessera can reach it (Docker network, DNS, etc.)
docker exec tessera curl -sf http://vt:10428/select/0/jaeger/api/services

# 3. Check tessera logs
journalctl -u tessera -n 100
docker logs tessera --tail 100
```

**Common causes:**

- Wrong URL in config (typo, wrong port)
- Network isolation (Docker network, firewall)
- DNS not resolving `vt` hostname (use FQDN or IP)
- Victoria not actually running

### Symptom: `/health/ready` returns 200 but `/api/traces` returns 502

**Cause:** Victoria reachable but query failing.

**Debug:** same as above, plus check:

```bash
# Look for Victoria API errors in tessera logs
journalctl -u tessera | grep -i "victoria\|502\|trace"
```

## Authentication issues

### Symptom: admin endpoints return 401

```
$ curl -i -X POST http://localhost:1990/api/dashboards
HTTP/1.1 401 Unauthorized
```

**Fix:** include bearer token:

```bash
curl -X POST http://localhost:1990/api/dashboards \
  -H "Authorization: Bearer $TESSERA_ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{ "id": "test", "title": "Test", ... }'
```

### Symptom: token rejected despite correct value

**Debug:**

```bash
# 1. Check token is set in tessera process
docker exec tessera env | grep TESSERA_ADMIN_TOKEN
# OR
systemctl show tessera | grep Environment

# 2. Check token in config is being read
docker logs tessera 2>&1 | grep -i "admin\|token"

# 3. Test token via curl with explicit value
curl -i -H "Authorization: Bearer abc123..." http://localhost:1990/api/dashboards
```

**Common causes:**

- Token has trailing whitespace (bash variable export issue)
- Different token in env vs secret file (one overrides the other)
- Token rotated but old value cached in client

## Slow responses

### Symptom: trace detail takes > 5 seconds

**Debug:**

```bash
# 1. Check Victoria response time directly
time curl -sf "http://vt:10428/select/0/jaeger/api/traces/abc123"

# 2. Check tessera logs for slow query warnings
journalctl -u tessera | grep "slow\|timeout"

# 3. Check tessera metrics (if /metrics enabled)
curl -sf http://localhost:1990/metrics | grep http_server_request_duration
```

**Common causes:**

- Victoria under load (high CPU, slow queries)
- Network latency between tessera and Victoria
- Single trace has 10k+ spans (waterfall rendering slow)
- Cache disabled but high traffic (every request hits Victoria)

### Symptom: dashboard list takes > 5 seconds

**Same as above** — check Victoria response time, then tessera logs.

## Logs not showing up

### Symptom: no logs in `journalctl` / docker logs

**Fix:**

```bash
# Check service is actually running
systemctl status tessera
docker ps | grep tessera

# Check log level configuration
# If too restrictive, set in config:
[telemetry]
log_level = "debug"   # verbose

# Check OpenTelemetry exporter config
# If OTLP endpoint is unreachable, logs are dropped silently
```

## High memory usage

### Symptom: tessera uses > 1GB RAM

**Debug:**

```bash
# Check actual memory
ps aux | grep tessera
docker stats tessera
```

**Common causes:**

- Many concurrent requests (each holds a TraceDetail in memory)
- Large trace trees (10k+ spans × ~500 bytes each = 5MB per request)
- Memory leak (should not happen, file a bug if it does)
- Insufficient GC tuning (large heap, infrequent collections)

**Mitigation:**

- Limit `limit` parameter on trace list queries
- Implement streaming for very large traces (stretch)
- Reduce dashboard refresh interval (don't poll every 5s)

## Storage issues

### Symptom: SQLite "database is locked"

```
[ERR] Microsoft.Data.Sqlite.SqliteException: SQLite Error 5: database is locked
```

**Cause:** SQLite serializes writes. Tessera should not have high write
contention in MVP (mostly reads), but bursts can cause this.

**Fix:**

- Reduce dashboard save frequency
- Move to PostgreSQL if contention persists (stretch)

### Symptom: disk full

**Fix:**

```bash
# Check disk usage
df -h /var/lib/tessera

# Clean up old logs (external logrotate)
journalctl --vacuum-time=7d

# Backup + delete old data
tessera-backup.sh
find /var/lib/tessera -name "*.db-wal" -size +1G -ls
```

## Common Victoria errors

### `vt: connection refused`

Tessera can't reach Victoria. Check:
- Victoria URL in config
- Network/firewall
- Victoria is running

### `vt: 401 Unauthorized`

Victoria requires a token that tessera didn't provide. Check:

```toml
[victoria.traces]
token = "env:TESSERA_VICTORIA_TOKEN"   # ← must not be empty
```

Verify token is correct:

```bash
curl -sf -H "Authorization: Bearer $TESSERA_VICTORIA_TOKEN" \
  http://vt:10428/select/0/jaeger/api/services
```

### `vl: 400 Bad Request`

LogsQL syntax error. Check the query:

```bash
# Test query directly
curl -sf -H "Authorization: Bearer $TESSERA_VICTORIA_TOKEN" \
  "http://vl:9428/select/logsql/query?query=*&limit=1"
```

### `vm: 422 Unprocessable Entity`

MetricsQL syntax error. Same as above, query directly.

## Debug mode

### Enable verbose logging

```toml
[telemetry]
log_level = "debug"
```

```bash
ASPNETCORE_ENVIRONMENT=Development tessera
```

### Capture network traffic

```bash
# Trace tessera → Victoria
tcpdump -i any -w tessera.pcap host vt and port 10428

# Or use mitmproxy for HTTP inspection
mitmproxy --mode reverse:http://vt:10428
```

### Inspect SQLite

```bash
sqlite3 /var/lib/tessera/tessera.db
sqlite> .tables
sqlite> .schema dashboard_versions
sqlite> SELECT * FROM dashboard_versions LIMIT 10;
```

## Getting help

1. Check tessera logs (`journalctl -u tessera` or `docker logs tessera`)
2. Check `/health/ready` for backend status
3. Check `/metrics` for performance data
4. Search existing issues: github.com/&lt;org&gt;/tessera/issues
5. Open new issue with:
   - tessera version (`tessera --version`)
   - OS / Docker version
   - Victoria versions (vt, vl, vm)
   - Config (with secrets redacted)
   - Logs (last 100 lines)
   - Reproduction steps

## Related docs

- `install.md` — deployment
- `configure.md` — env vars, secrets
- `../architecture/log-format.md` — log format
- `../architecture/config-format.md` — TOML schema