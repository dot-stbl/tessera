# Victoria Stack — API Reference

> Authoritative reference for the HTTP APIs tessera talks to. VictoriaTraces is
> Jaeger-compatible; VictoriaLogs uses LogsQL; VictoriaMetrics is Prometheus-compatible.
>
> All endpoints accept single-tenant path: `0` in MVP. Auth via `Authorization: Bearer <token>`.

## VictoriaTraces (Jaeger-compatible API)

**Base URL:** `http://<host>:10428`
**Path prefix:** `/select/jaeger/api/`
**Auth:** `Authorization: Bearer <token>` (optional, if configured on vtselect)

### Endpoints tessera uses

#### `GET /select/jaeger/api/services`

List all services that have produced traces.

**Request:**

```
GET /select/jaeger/api/services HTTP/1.1
Host: vt:10428
Authorization: Bearer <token>
```

**Response:**

```json
{
  "data": ["checkout-api", "postgres", "stripe", "frontend"],
  "total": 4,
  "limit": 0,
  "offset": 0,
  "errors": null
}
```

#### `GET /select/jaeger/api/services/{service_name}/operations`

List all operations for a given service.

**Request:**

```
GET /select/jaeger/api/services/checkout-api/operations HTTP/1.1
```

**Response:**

```json
{
  "data": ["POST /checkout", "GET /cart", "POST /payment"],
  "total": 3,
  "limit": 0,
  "offset": 0,
  "errors": null
}
```

#### `GET /select/jaeger/api/traces` (search)

Search traces by filters. Default `limit=20`.

**Query parameters:**

| Param | Type | Required | Notes |
|-------|------|----------|-------|
| `service` | string | optional | Service name to filter on |
| `operation` | string | optional | Operation name to filter on |
| `tags` | string | optional | JSON-encoded `{"key": "value", ...}`. Special prefixes: `resource_attr:`, `scope_attr:` |
| `start` | int (unix ms) | optional | Start of time range (inclusive) |
| `end` | int (unix ms) | optional | End of time range (exclusive) |
| `minDuration` | string | optional | Min duration, units: `ns`/`us`/`ms`/`s`/`m`/`h`. e.g. `100ms` |
| `maxDuration` | string | optional | Max duration, same units |
| `limit` | int | optional | Max traces to return (default 20) |

**Example:**

```
GET /select/jaeger/api/traces?service=checkout-api&start=1721337600000&end=1721341200000&minDuration=100ms&limit=50
```

**Response (Jaeger trace format):**

```json
{
  "data": [
    {
      "traceID": "9e06226196051d9c3c10dfab343791ad",
      "spans": [
        {
          "traceID": "9e06226196051d9c3c10dfab343791ad",
          "spanID": "42e4324fcb045b99",
          "operationName": "Currency/Convert",
          "processID": "p12",
          "startTime": 1750044449711715,
          "duration": 655,
          "tags": [
            { "key": "span.kind", "type": "string", "value": "server" },
            { "key": "http.method", "type": "string", "value": "POST" },
            { "key": "http.status_code", "type": "string", "value": "200" },
            { "key": "error", "type": "string", "value": "false" }
          ],
          "logs": [
            {
              "timestamp": 1750044449711719,
              "fields": [
                { "key": "event", "type": "string", "value": "Conversion successful" }
              ]
            }
          ],
          "references": [
            {
              "refType": "CHILD_OF",
              "spanID": "34a9d7aa3afe1688",
              "traceID": "9e06226196051d9c3c10dfab343791ad"
            }
          ],
          "warnings": null
        }
      ],
      "processes": {
        "p12": {
          "serviceName": "currency",
          "tags": [
            { "key": "otel.scope.name", "type": "string", "value": "currency" }
          ]
        }
      },
      "warnings": null
    }
  ],
  "total": 1,
  "limit": 0,
  "offset": 0,
  "errors": null
}
```

**Note:** search results return **first span per trace**, not full trace tree.
For full tree, fetch by trace_id.

#### `GET /select/jaeger/api/traces/{trace_id}`

Get full trace by ID.

**Request:**

```
GET /select/jaeger/api/traces/9e06226196051d9c3c10dfab343791ad HTTP/1.1
```

**Response:** Same shape as search, but `data[0].spans` is the complete span set for the trace.

**Tessera's job:** reconstruct parent/child tree from `references[]` (CHILD_OF type).

#### `GET /select/jaeger/api/dependencies` (stretch)

Get service-to-service dependencies.

**Response:** Adjacency matrix of (parent, child, call_count).

---

### Tags filter — `resource_attr:` and `scope_attr:` prefixes

Filter by span attributes, resource attributes, or scope attributes:

```
?tags={"http.status_code":"500"}
?tags={"resource_attr:service.namespace":"production"}
?tags={"scope_attr:otel.library.version":"1.30.0"}
```

| Prefix | Matches |
|--------|---------|
| (none) | Span attribute |
| `resource_attr:` | Resource attribute (e.g. `service.name`) |
| `scope_attr:` | Instrumentation scope attribute |

---

### Internal representation (also accessible via VictoriaTraces query path)

VictoriaTraces stores spans as flat records with fields like:

```
_stream         = "{name=\"tcp.connect\",resource_attr:service.name=\"payment\"}"
_time           = "2025-06-16T03:26:48.796828584Z"
_msg            = "-"
trace_id        = "769d28c4b8633dc9de2cc421d1a1616f"
span_id         = "2a1f3c4bda1d0e43"
parent_span_id  = "3ea61f2d2a5a003d"
name            = "tcp.connect"
kind            = "1"        # 1=INTERNAL, 2=SERVER, 3=CLIENT, 4=PRODUCER, 5=CONSUMER
status_code     = "0"        # 0=UNSET, 1=OK, 2=ERROR
flags           = "0"
start_time_unix_nano = "1750044408780000000"
end_time_unix_nano   = "1750044408796828584"
duration        = "16828584"  # nanoseconds
resource_attr:* = <resource attributes>
span_attr:*     = <span attributes>
scope_name      = "@opentelemetry/instrumentation-net"
scope_version   = "0.43.1"
```

**Tessera uses Jaeger API** (cleaner, standard), not internal representation. Internal is fallback.

---

## VictoriaLogs (LogsQL)

**Base URL:** `http://<host>:9428`
**Path prefix:** `/select/logsql/`
**Auth:** `Authorization: Bearer <token>` (optional)

### Endpoint tessera uses

#### `GET /select/logsql/query` (and `POST`)

Query logs by LogsQL expression.

**Query parameters:**

| Param | Type | Required | Notes |
|-------|------|----------|-------|
| `query` | string | **required** | LogsQL expression |
| `limit` | int | optional | Max log entries (no default cap, **tessera caps at 500**) |
| `offset` | int | optional | Skip entries (requires `limit`) |
| `start` | string | optional | RFC3339 / ISO8601 start time. e.g. `2025-01-01T00:00:00Z` |
| `end` | string | optional | RFC3339 / ISO8601 end time |

**Example — find logs for a trace:**

```
GET /select/logsql/query?query=trace_id%3A%22abc123def456%22&start=2025-01-01T00:00:00Z&end=2025-01-01T01:00:00Z&limit=500
```

**Response: NDJSON** (newline-delimited JSON, one log per line):

```json
{"_msg":"failed to charge card","_stream":"{app=\"checkout-api\"}","_time":"2025-01-01T00:05:23Z","level":"ERROR","trace_id":"abc123def456","span_id":"span_001","error.kind":"StripeError","user_id":"u_42"}
{"_msg":"payment retried","_stream":"{app=\"checkout-api\"}","_time":"2025-01-01T00:05:24Z","level":"INFO","trace_id":"abc123def456","span_id":"span_001","attempt":"2"}
...
```

Each line is one log entry. Fields:
- `_msg` — the log message body
- `_stream` — stream identifier (typically `{app="name"}` or `{resource_attr:service.name="..."}`)
- `_time` — RFC3339 timestamp
- All other fields are dynamic (level, trace_id, span_id, app-specific tags)

**POST variant:** same params, body contains LogsQL query (for long queries).

### LogsQL basics

```
_trace_id:"abc123def456"             # exact match on trace_id field
_stream:{app="checkout-api"}         # filter by stream field
level:ERROR                          # exact match on level field
error.kind:*Stripe*                  # wildcard substring match
*                                   # match all
```

**Stream vs field distinction:**
- `_stream:{...}` filters by stream identifier (which log stream/sources)
- `field:value` filters by field on log entries (level, trace_id, etc.)

**Time filter shorthand** (alternative to start/end params):

```
_time:5m            # last 5 minutes
_time:1h            # last hour
_time:2025-01-01    # since midnight Jan 1 2025
```

### Other endpoints (for stretch / diagnostics)

- `GET /select/logsql/stream_field_names` — discover stream fields
- `GET /select/logsql/field_names` — discover field names
- `GET /select/logsql/hits` — same as query, returns hits stats
- `GET /health` — VictoriaLogs health

---

## VictoriaMetrics (Prometheus-compatible)

**Base URL:** `http://<host>:8429`
**Path prefix:** `/prometheus/api/v1/` (or just `/api/v1/`)
**Auth:** `Authorization: Bearer <token>` (optional)

### Endpoints tessera uses (stretch scope)

#### `GET /api/v1/query_range`

Range query for metrics over time.

**Query parameters:**

| Param | Type | Required | Notes |
|-------|------|----------|-------|
| `query` | string | **required** | MetricsQL expression |
| `start` | string | **required** | RFC3339 or unix seconds |
| `end` | string | optional | RFC3339 or unix seconds (default: now) |
| `step` | string | optional | Interval between data points, default `5m` |
| `timeout` | string | optional | e.g. `5s` |

**Example — error rate over last hour:**

```
GET /prometheus/api/v1/query_range?query=sum(rate(http_requests_total{status=~"5.."}[5m]))&start=2025-01-01T00:00:00Z&end=2025-01-01T01:00:00Z&step=1m
```

**Response:**

```json
{
  "status": "success",
  "data": {
    "resultType": "matrix",
    "result": [
      {
        "metric": { "__name__": "http_requests_total" },
        "values": [
          [1735689600, "42"],
          [1735689660, "37"],
          ...
        ]
      }
    ]
  }
}
```

#### `GET /api/v1/query` (instant)

Single-point query.

```
GET /prometheus/api/v1/query?query=up&time=2025-01-01T00:00:00Z
```

#### `GET /api/v1/labels`

List all label names.

#### `GET /api/v1/label/{name}/values`

List values for a label.

#### `GET /api/v1/series`

Find series by label matchers.

```
GET /prometheus/api/v1/series?match[]=up&match[]=process_start_time_seconds
```

### VictoriaMetrics extras

| Extra | Effect |
|-------|--------|
| `round_digits=N` | Round response values to N decimal places |
| `limit=N` | Limit results for `/labels`, `/label/.../values`, `/series` |
| `stats` in response | Adds `executionTimeMsec`, `seriesFetched` |

### MetricsQL basics (extends PromQL)

```
http_requests_total                    # instant value
rate(http_requests_total[5m])          # per-second rate over 5m
sum(rate(http_requests_total[5m]))     # sum across labels
http_requests_total{status="500"}      # filter by label
http_requests_total{status=~"5.."}     # regex match
histogram_quantile(0.95, ...)          # percentile from histogram
```

---

## Time formats — cross-system summary

| System | Format | Example |
|--------|--------|---------|
| VictoriaTraces (`start`/`end` query params) | unix **milliseconds** (int) | `1721337600000` |
| VictoriaTraces (`startTime`/`duration` in response) | unix **microseconds** (int) | `1750044449711715` |
| VictoriaLogs (`start`/`end` query params) | **RFC3339 / ISO8601** string | `2025-01-01T00:00:00Z` |
| VictoriaLogs (`_time` field) | RFC3339 string | `2025-01-01T00:00:00.123456789Z` |
| VictoriaMetrics (`start`/`end` query params) | RFC3339 string OR unix seconds | `2025-01-01T00:00:00Z` or `1735689600` |
| VictoriaMetrics response `values[][]` | unix **seconds** (number) | `1735689600` |

**Tessera convention:** Use **UTC unix ms everywhere on the wire** between backend and frontend. Convert at the Victoria boundary.

## Tenant routing

Victoria supports multi-tenancy via path prefix:

```
/select/0/jaeger/api/services       # tenant 0
/select/42/jaeger/api/services      # tenant 42
/select/0/logsql/query              # tenant 0
```

**Tessera MVP:** hardcode tenant `0`. Single-tenant deployment.

For multi-tenant, tenant comes from:
- Config: `Victoria:Tenant: "0"` per-source
- Or runtime resolution via auth claims (out of MVP scope)

## Auth

All Victoria endpoints accept bearer token via `Authorization: Bearer <token>` header.

**Tessera pattern:**
- Token configured in `appsettings.json` or env var `Victoria__Token`
- Backend stores token, never exposes to SPA
- SPA → backend → victoria (token stays server-side)

```
Tessera.Host
├── appsettings.json: Victoria.Token = "secret-xxx"
├── IVictoriaTracesClient: includes Authorization header on every call
└── SPA only sees /api/* endpoints (no token leak)
```

## CORS

If Victoria and Tessera run on different origins, CORS must be configured on
Victoria side. Typical setup:

```
# vtselect / vmselect / vlselect flags
-flag=httpListenAddr=:10428
```

For browser-direct calls (NOT tessera's pattern, but if doing):
```
- http.cors.origin=*
```

**Tessera does backend-proxy pattern** — no CORS needed because browser → Tessera only.

## Error responses

Victoria returns:

| Status | Meaning |
|--------|---------|
| 200 | OK (even with empty `data: []`) |
| 400 | Bad request (malformed LogsQL, invalid params) |
| 401 | Auth required / invalid token |
| 500 | Internal Victoria error |

Error response body:

```json
{
  "error": "error message here"
}
```

**Tessera error handling:** surface as `Result<T, Error>` in handlers, map to HTTP 4xx/5xx in endpoints.

## Quick URL cheatsheet

| Purpose | URL |
|---------|-----|
| List services | `GET {base}/select/jaeger/api/services` |
| List operations | `GET {base}/select/jaeger/api/services/{svc}/operations` |
| Search traces | `GET {base}/select/jaeger/api/traces?service=...&start=...&end=...&limit=...` |
| Get trace | `GET {base}/select/jaeger/api/traces/{trace_id}` |
| Service deps (stretch) | `GET {base}/select/jaeger/api/dependencies` |
| Logs query | `GET {base}/select/logsql/query?query=...&start=...&end=...&limit=...` |
| Metrics range | `GET {base}/prometheus/api/v1/query_range?query=...&start=...&end=...&step=...` |
| Metrics labels | `GET {base}/prometheus/api/v1/labels` |

Replace `{base}` with the actual Victoria endpoint (e.g., `http://vt:10428`).

## Sources

- [VictoriaTraces querying docs](https://docs.victoriametrics.com/victoriatraces/querying/)
- [VictoriaLogs querying docs](https://docs.victoriametrics.com/victorialogs/querying/)
- [VictoriaMetrics querying API](https://docs.victoriametrics.com/victoriametrics/querying/)
- [VictoriaLogs LogsQL reference](https://docs.victoriametrics.com/victorialogs/logsql/)
