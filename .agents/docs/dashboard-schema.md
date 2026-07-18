# Dashboard schema

> **Decision (locked 2026-07-19):** Custom JSON format, versioned, Grafana-inspired structure.

Tessera dashboards are JSON files with a custom schema inspired by Grafana's
layout. The format is **versioned** (`schemaVersion`) and **declarative**
(editable in any text editor, version-controllable in git). Frontend and
backend both understand the same schema.

## Schema versioning

```json
{
  "schemaVersion": "1",
  ...
}
```

- `schemaVersion: "1"` — current schema version
- Backend validates on load; rejects unknown versions with clear error
- Breaking changes bump the version; backward-compatible additions don't

## Full schema example

```json
{
  "schemaVersion": "1",
  "id": "service-overview",
  "title": "Service Overview",
  "description": "Default view for any service",
  "tags": ["apm", "default"],

  "timeRange": { "from": "now-1h", "to": "now" },
  "refresh": "30s",
  "timezone": "browser",

  "variables": [
    {
      "name": "service",
      "label": "Service",
      "type": "query",
      "datasource": "vt",
      "query": "services",
      "multi": false,
      "includeAll": false
    },
    {
      "name": "env",
      "label": "Environment",
      "type": "custom",
      "options": ["prod", "staging"],
      "current": "prod"
    }
  ],

  "panels": [
    {
      "id": "p1",
      "type": "trace_list",
      "title": "Recent traces for $service",
      "gridPos": { "x": 0, "y": 0, "w": 24, "h": 8 },
      "datasource": "vt",
      "query": {
        "type": "traces.search",
        "service": "$service",
        "status": "any",
        "limit": 50
      }
    },
    {
      "id": "p2",
      "type": "log_view",
      "title": "Error logs for $service",
      "gridPos": { "x": 0, "y": 8, "w": 12, "h": 8 },
      "datasource": "vl",
      "query": {
        "type": "logs.query",
        "logsql": "service:\"$service\" AND level:ERROR"
      }
    },
    {
      "id": "p3",
      "type": "metric_chart",
      "title": "Request rate for $service",
      "gridPos": { "x": 12, "y": 8, "w": 12, "h": 8 },
      "datasource": "vm",
      "query": {
        "type": "metrics.range",
        "metricsql": "sum(rate(http_requests_total{service=\"$service\"}[5m]))"
      }
    },
    {
      "id": "p4",
      "type": "markdown",
      "title": "About",
      "gridPos": { "x": 0, "y": 16, "w": 24, "h": 4 },
      "content": "# Service Overview\n\nThis dashboard shows traces and logs for **$service** in **$env**."
    }
  ]
}
```

## Field reference

### Top-level

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `schemaVersion` | string | **yes** | Currently `"1"` |
| `id` | string | **yes** | URL-safe identifier (kebab-case) |
| `title` | string | **yes** | Display title |
| `description` | string | no | Subtitle shown in dashboard header |
| `tags` | string[] | no | For filtering/grouping |
| `timeRange` | object | yes | Default `{from, to}` (e.g. `"now-1h"`, `"now"`) |
| `refresh` | string | no | Auto-refresh interval (`"30s"`, `"1m"`, etc.) or `"off"` |
| `timezone` | string | no | `"browser"` (default) or IANA name |
| `variables` | array | no | Template variables |
| `panels` | array | **yes** | At least one panel required |

### Variables

```json
{
  "name": "service",
  "label": "Service",
  "type": "query",                     // query | custom
  "datasource": "vt",                  // for query type
  "query": "services",                 // for query type
  "multi": false,                      // allow multiple selection
  "includeAll": false,                 // add "All" option
  "options": ["prod", "staging"],      // for custom type
  "current": "prod"                    // for custom type
}
```

Reference a variable in queries: `$service` or `${service}` (escaped for
queries containing `$` literal).

### Panels

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `id` | string | yes | Panel-unique identifier within dashboard |
| `type` | string | yes | `trace_list`, `log_view`, `metric_chart`, `service_map`, `markdown`, etc. |
| `title` | string | yes | Panel title |
| `gridPos` | object | yes | `{x, y, w, h}` — 24-column grid |
| `datasource` | string | no | `"vt"`, `"vl"`, `"vm"` — required for query panels |
| `query` | object | no | Discriminated by `query.type` |
| `content` | string | no | For `markdown` panels |
| `options` | object | no | Panel-specific options (color, unit, etc.) |

### Grid position

```
┌─────────────────────────────────────────────────────────┐
│ x=0,y=0, w=24, h=4                                      │  ← full-width header
├────────────────────────────────┬────────────────────────┤
│ x=0,y=4, w=12, h=8             │ x=12,y=4, w=12, h=8    │
│ (trace list)                   │ (log view)             │
├────────────────────────────────┴────────────────────────┤
│ x=0,y=12, w=24, h=6                                     │  ← full-width chart
└─────────────────────────────────────────────────────────┘
```

- `x`, `y`: top-left position
- `w`, `h`: width (max 24) and height in grid units
- 1 unit ≈ 30px in default rendering

### Query types

Discriminated union by `query.type`:

| `type` | `datasource` | Used by panel |
|--------|--------------|---------------|
| `traces.search` | `vt` | `trace_list` |
| `traces.get` | `vt` | `trace_detail` |
| `logs.query` | `vl` | `log_view` |
| `metrics.range` | `vm` | `metric_chart` |
| `metrics.instant` | `vm` | `metric_stat` |
| `dependencies` | `vt` | `service_map` |

## Panel types catalog

| type | backend query | UI render | MVP? |
|------|---------------|-----------|------|
| `trace_list` | `vt /select/jaeger/api/traces` | table | ✅ |
| `trace_detail` | `vt /select/jaeger/api/traces/{id}` | waterfall | ✅ |
| `log_view` | `vl /select/logsql/query` | log list | ✅ |
| `metric_chart` | `vm /prometheus/api/v1/query_range` | time series | ✅ |
| `metric_stat` | `vm /prometheus/api/v1/query` | big number | ✅ |
| `markdown` | (no query) | rendered text | ✅ |
| `service_map` | `vt /select/jaeger/api/dependencies` | graph | stretch |
| `flame_graph` | (computed) | icicle | stretch |

## C# model

```csharp
namespace Tessera.Modules.Dashboards;

public sealed record Dashboard
{
    public required string SchemaVersion { get; init; }
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
    public required TimeRange TimeRange { get; init; }
    public string Refresh { get; init; } = "off";
    public string Timezone { get; init; } = "browser";
    public IReadOnlyList<DashboardVariable> Variables { get; init; } = [];
    public IReadOnlyList<DashboardPanel> Panels { get; init; } = [];
}

public sealed record TimeRange(string From, string To);

public sealed record DashboardVariable
{
    public required string Name { get; init; }
    public string? Label { get; init; }
    public required string Type { get; init; }     // query | custom
    public string? Datasource { get; init; }      // vt | vl | vm
    public string? Query { get; init; }
    public bool Multi { get; init; }
    public bool IncludeAll { get; init; }
    public IReadOnlyList<string>? Options { get; init; }
    public string? Current { get; init; }
}

public sealed record DashboardPanel
{
    public required string Id { get; init; }
    public required string Type { get; init; }
    public required string Title { get; init; }
    public required GridPosition GridPos { get; init; }
    public string? Datasource { get; init; }
    public PanelQuery? Query { get; init; }
    public string? Content { get; init; }     // for markdown
    public IReadOnlyDictionary<string, object>? Options { get; init; }
}

public sealed record GridPosition(int X, int Y, int W, int H);

public sealed record PanelQuery
{
    public required string Type { get; init; }
    // Remaining fields depend on Type — see below
}
```

## Variable substitution

Backend resolves variables before executing queries:

```csharp
public sealed class VariableResolver
{
    public string Resolve(string template, IReadOnlyDictionary<string, string> variables)
    {
        return Regex.Replace(template, @"\$(\w+)", match =>
        {
            var name = match.Groups[1].Value;
            return variables.TryGetValue(name, out var value)
                ? value
                : match.Value;  // leave unresolvable variables as-is
        });
    }
}
```

Example: `"service:\"$service\""` with `service = "checkout-api"` →
`"service:\"checkout-api\""`.

## Storage

Dashboards as JSON files in `data_dir/dashboards/`:

```
data_dir/dashboards/
├── default/
│   ├── service-overview.json
│   └── error-investigation.json
└── custom/
    └── my-team-dashboard.json
```

See `operations/storage.md` for full layout.

## API endpoints

| Method | Path | Auth | Purpose |
|--------|------|------|---------|
| `GET` | `/api/dashboards` | guest | List dashboards (metadata only) |
| `GET` | `/api/dashboards/{id}` | guest | Get full dashboard JSON |
| `POST` | `/api/dashboards` | admin | Create new dashboard |
| `PUT` | `/api/dashboards/{id}` | admin | Update dashboard (creates new version) |
| `DELETE` | `/api/dashboards/{id}` | admin | Delete dashboard |

## Editor UI (MVP scope)

MVP editor is **basic**:
- Add/remove panels
- Edit panel title, type, grid position (drag-resize)
- Edit query (form fields, not raw JSON editing)
- Save → writes JSON file

**Out of MVP editor:**
- Visual query builder (LogsQL/MetricsQL autocomplete)
- Multi-dashboard editing
- Version diff UI
- Bulk operations

## Anti-patterns

```json
❌ Missing schemaVersion
❌ Hardcoded secrets in queries
❌ Schema references unknown panel type
❌ Variable name doesn't match `[a-z][a-z0-9_]*`
❌ Grid position outside 24-col grid (w > 24)
❌ Duplicate panel IDs within same dashboard
```

## Test requirements

- `tests/unit/modules/Tessera.Modules.Dashboards.Unit/`:
  - `DashboardTests.Deserialize_ValidJson`
  - `DashboardTests.Deserialize_RejectsUnknownSchemaVersion`
  - `VariableResolverTests.SubstitutesKnownVariables`
  - `VariableResolverTests.LeavesUnknownVariablesAsIs`
- Integration: load real dashboard from `dashboards/default/`, render in WebApplicationFactory

## Related docs

- `architecture.md` — overall architecture
- `operations/storage.md` — where dashboard files live
- `security/auth-model.md` — admin authorization for write endpoints
- `../rules/coding/naming-and-types.md` — sealed records, primary constructors
- `ui/dashboards.md` — UI rendering of dashboard schema (forthcoming)