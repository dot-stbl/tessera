# Storage

> **Decision (locked 2026-07-19):** SQLite + JSON files. No PostgreSQL.

Tessera stores metadata in SQLite, dashboards as JSON files. Inspired by
Grafana's local-first model: zero infrastructure, single-binary deploy, all
data on disk for backup/version-control.

## Layout

```
/var/lib/tessera/                    # data_dir from [storage] config
├── tessera.db                       # SQLite — users, permissions, dashboard metadata
├── dashboards/                      # JSON files — declarative dashboards
│   ├── default/
│   │   ├── service-overview.json
│   │   └── error-investigation.json
│   └── custom/
│       └── my-team-dashboard.json
└── logs/                            # rotated by external logrotate

/etc/tessera/                        # config_dir (separate from data)
├── tessera.toml
└── tessera.local.toml
```

## SQLite — runtime metadata

Holds:

| Table | Purpose |
|-------|---------|
| `users` | Admin users (when added) |
| `sessions` | Active sessions |
| `dashboard_versions` | Version history of dashboards (if enabled) |
| `audit_log` | Sensitive operations log (stretch) |

Dashboards themselves are NOT in SQLite — they're in JSON files. SQLite
only tracks pointers + version metadata.

## JSON files — dashboards

```json
{
  "schemaVersion": "1",
  "id": "service-overview",
  "title": "Service Overview",
  ...
}
```

Files are:
- Human-readable
- Editable in any text editor
- Version-controllable in git
- Hot-reloadable (future)

## File path resolution

```toml
[storage]
data_dir = "/var/lib/tessera"
# dashboards_dir defaults to {data_dir}/dashboards
# logs_dir defaults to {data_dir}/logs
```

Override at runtime via env var:
```bash
TESSERA_STORAGE__DATA_DIR=/custom/path tessera
```

## C# access layer

```csharp
namespace Tessera.Shared.Storage;

public sealed class StorageOptions
{
    public required string DataDir { get; init; }
    public string DashboardsDir => Path.Combine(DataDir, "dashboards");
    public string LogsDir => Path.Combine(DataDir, "logs");
    public string DatabasePath => Path.Combine(DataDir, "tessera.db");
}

public sealed class StorageLayout(
    IOptions<StorageOptions> options,
    ILogger<StorageLayout> logger)
{
    private readonly StorageOptions _opts = options.Value;

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(_opts.DataDir);
        Directory.CreateDirectory(_opts.DashboardsDir);
        Directory.CreateDirectory(_opts.LogsDir);
        logger.LogInformation(
            "Storage initialized at {DataDir}",
            _opts.DataDir);
    }

    public string DashboardPath(string id) =>
        Path.Combine(_opts.DashboardsDir, $"{id}.json");
}
```

## Dashboard loader

```csharp
public sealed class DashboardLoader(
    StorageLayout storage,
    ILogger<DashboardLoader> logger)
{
    public async Task<Dashboard?> LoadAsync(string id, CancellationToken ct)
    {
        var path = storage.DashboardPath(id);
        if (!File.Exists(path))
        {
            logger.LogWarning("Dashboard {DashboardId} not found at {Path}", id, path);
            return null;
        }

        await using var stream = File.OpenRead(path);
        var dashboard = await JsonSerializer.DeserializeAsync<Dashboard>(
            stream, JsonOpts.Default, ct);

        return dashboard;
    }

    public async Task SaveAsync(Dashboard dashboard, CancellationToken ct)
    {
        var path = storage.DashboardPath(dashboard.Id);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, dashboard, JsonOpts.Default, ct);
        logger.LogInformation("Dashboard {DashboardId} saved to {Path}", dashboard.Id, path);
    }
}
```

## SQLite via EF Core

```csharp
// src/shared/Tessera.Shared.Storage/Persistence/TesseraDbContext.cs
namespace Tessera.Shared.Storage.Persistence;

public sealed class TesseraDbContext(DbContextOptions<TesseraDbContext> options)
    : DbContext(options)
{
    public DbSet<DashboardVersionEntity> DashboardVersions => Set<DashboardVersionEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DashboardVersionEntity>(e =>
        {
            e.ToTable("dashboard_versions");
            e.HasKey(v => v.Id);
            e.Property(v => v.Id).HasColumnName("id");
            e.Property(v => v.DashboardId).HasColumnName("dashboard_id");
            e.Property(v => v.Version).HasColumnName("version");
            e.Property(v => v.CreatedAt).HasColumnName("created_at");
        });
    }
}

public sealed class DashboardVersionEntity
{
    public required string Id { get; init; }
    public required string DashboardId { get; init; }
    public required int Version { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}
```

```csharp
// src/host/Tessera.Host/Program.cs
builder.Services.AddDbContext<TesseraDbContext>(opts =>
    opts.UseSqlite($"Data Source={storageOptions.DatabasePath}"));
```

## Backup strategy (stretch)

- **Manual:** `tar czf tessera-backup.tgz /var/lib/tessera/`
- **Filesystem snapshots:** LVM/ZFS snapshots, btrfs send/receive
- **Dashboard versioning:** SQLite `dashboard_versions` table keeps last N revisions
- **Automated:** systemd timer running `tessera backup` (stretch command)

## Why no PostgreSQL

- Self-hosted single-tenant doesn't need multi-connection concurrency
- SQLite handles 100k+ writes/sec in WAL mode (more than tessera needs)
- Zero infrastructure: no separate DB process, no connection pool tuning
- File-based = trivial backup (copy file)
- Single binary deploy maintained

If tessera grows to multi-tenant SaaS, PostgreSQL becomes useful (concurrent
writes, replication, point-in-time recovery). For MVP, SQLite is right.

## Anti-patterns

```csharp
// ❌ WRONG — hardcoded paths in code
var path = "/var/lib/tessera/dashboards/foo.json";

// ❌ WRONG — storing dashboards in SQLite blob column
modelBuilder.Entity<DashboardEntity>()
    .Property(d => d.Json).HasColumnType("jsonb");

// ❌ WRONG — bypassing StorageLayout, direct File API
File.ReadAllText("/etc/tessera/dashboards/" + id + ".json");
```

## Test requirements

- `tests/unit/core/Tessera.Shared.Storage.Unit/`:
  - `StorageLayoutTests.EnsureDirectories_CreatesAll`
  - `StorageLayoutTests.DashboardPath_Resolves`
  - `DashboardLoaderTests.LoadAsync_FileNotFound_ReturnsNull`
  - `DashboardLoaderTests.SaveAsync_WritesFile`
- Integration: Testcontainers not needed (file system)

## Related docs

- `architecture.md` — overall architecture
- `dashboard-schema.md` — JSON schema for dashboard files
- `../rules/coding/naming-and-types.md` — sealed classes, primary constructors