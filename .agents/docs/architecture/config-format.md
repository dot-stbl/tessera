# Configuration format

> **Decision (locked 2026-07-19):** TOML only. No `appsettings.json` in source.

Tessera loads configuration from TOML files. Strict schema, comments, XDG-style
paths, no .NET-isms leaking into deploy artifacts. Secrets are in env vars,
not in TOML files.

## Path resolution

`tessera` searches for `tessera.toml` in this order (first wins):

```
1. $TESSERA_CONFIG env var (explicit override)
2. /etc/tessera/tessera.toml (production Linux)
3. $XDG_CONFIG_HOME/tessera/tessera.toml (user-level, default ~/.config/tessera/)
4. ./tessera.toml (dev)
```

## TOML schema basics

```toml
# Comment (TOML supports #)
version = "1"           # REQUIRED at root

[server]                # table (nested object) — port 1990 per coding/project-ports.md
host = "0.0.0.0"
port = 1990

[victoria.traces]       # nested table
url = "http://vt:10428"
tenant = "0"
token = "env:TESSERA_VICTORIA_TOKEN"  # see Secrets section

[[datasources]]         # array of tables (future)
name = "production"
type = "vt"
```

## Schema versioning

**Every `tessera.toml` MUST start with `version = "1"`.** Backend validates
on startup:

- `version` lower than supported → fail with migration message
- `version` higher than supported → fail with upgrade message
- No silent migration. Manual upgrade per CHANGELOG.

## Secrets — environment variables only

**NEVER put secrets in `tessera.toml`** — TOML files are version-controlled.
Use one of:

```toml
[victoria.traces]
# Pattern A: env var reference (recommended)
token = "env:TESSERA_VICTORIA_TOKEN"

# Pattern B: file reference
token_file = "/etc/tessera/secrets/vt-token"
```

Backend resolves at startup:
- `env:VAR_NAME` → reads `VAR_NAME` from environment
- `file:/path` → reads file, trims whitespace
- literal string (no prefix) → used as-is (dev only)

## Options binding (C# options pattern)

Options classes follow `naming-and-types.md` and `project-naming-and-setup.md`:

```csharp
namespace Tessera.Shared.Configuration;

public sealed class VictoriaOptions
{
    public required string Tenant { get; init; } = "0";
    public int TimeoutMs { get; init; } = 5000;
    public VictoriaTracesOptions Traces { get; init; } = new();
    public VictoriaLogsOptions Logs { get; init; } = new();
    public VictoriaMetricsOptions Metrics { get; init; } = new();
}

public sealed class VictoriaTracesOptions
{
    public required string Url { get; init; }
    public string Tenant { get; init; } = "0";

    public string? TokenRaw { get; init; }

    public string? Token => TokenRaw switch
    {
        null => null,
        { } raw when raw.StartsWith("env:") =>
            Environment.GetEnvironmentVariable(raw[4..]),
        { } raw when raw.StartsWith("file:") =>
            File.ReadAllText(raw[5..]).Trim(),
        _ => TokenRaw
    };
}
```

## Validation on startup

```csharp
// src/host/Tessera.Host/Program.cs
builder.Services
    .AddOptions<VictoriaOptions>()
    .Bind(builder.Configuration.GetSection("victoria"))
    .ValidateOnStart();   // fail-fast at startup, not on first request

builder.Services.AddSingleton<
    IValidateOptions<VictoriaOptions>, VictoriaOptionsValidator>();

public sealed class VictoriaOptionsValidator : IValidateOptions<VictoriaOptions>
{
    public ValidateOptionsResult Validate(string? name, VictoriaOptions options)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(options.Traces.Url))
            errors.Add("victoria.traces.url is required");
        if (string.IsNullOrWhiteSpace(options.Logs.Url))
            errors.Add("victoria.logs.url is required");
        if (options.TimeoutMs < 100)
            errors.Add("victoria.timeout_ms must be >= 100");
        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }
}
```

## TOML → IConfiguration binding

`src/shared/Tessera.Shared.Configuration/TomlConfigurationProvider.cs`:

```csharp
namespace Tessera.Shared.Configuration;

public sealed class TomlConfigurationProvider : FileConfigurationProvider
{
    public TomlConfigurationProvider(TomlConfigurationSource source) : base(source) { }

    public override void Load(Stream stream)
    {
        var toml = Toml.Parse(stream);
        var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        FlattenTable(toml, prefix: "", data);
        Data = data;
    }

    private static void FlattenTable(
        TomlTable table,
        string prefix,
        Dictionary<string, string?> data)
    {
        foreach (var (key, value) in table)
        {
            var path = string.IsNullOrEmpty(prefix) ? key : $"{prefix}:{key}";
            FlattenValue(value, path, data);
        }
    }

    private static void FlattenValue(
        object? value,
        string path,
        Dictionary<string, string?> data)
    {
        switch (value)
        {
            case TomlTable nested:
                FlattenTable(nested, path, data);
                break;
            case TomlArray array:
                for (var i = 0; i < array.Count; i++)
                    FlattenValue(array[i], $"{path}:{i}", data);
                break;
            default:
                data[path] = value?.ToString();
                break;
        }
    }
}
```

## Multiple files (layered config)

Grafana-style layered loading:

```
/etc/tessera/
├── tessera.toml            # main config (committed, versioned)
├── tessera.local.toml      # local override (gitignored, env-specific)
```

`tessera.local.toml` is loaded **after** main; later values win. Same schema.
Use case: shared base config in git + per-host overrides in
`tessera.local.toml` (secrets, env-specific URLs).

## Example `tessera.toml`

```toml
# Tessera configuration
version = "1"

[server]
host = "0.0.0.0"
port = 1990

[victoria]
tenant = "0"
timeout_ms = 5000

[victoria.traces]
url = "http://vt:10428"
token = "env:TESSERA_VICTORIA_TOKEN"

[victoria.logs]
url = "http://vl:9428"
token = "env:TESSERA_VICTORIA_TOKEN"

[victoria.metrics]
url = "http://vm:8429"
token = "env:TESSERA_VICTORIA_TOKEN"

[storage]
data_dir = "/var/lib/tessera"

[auth]
guest_enabled = true
admin_token_source = "env:TESSERA_ADMIN_TOKEN"

[telemetry]
service_name = "tessera"
log_level = "info"             # trace | debug | info | warn | error
log_format = "dot.case"        # dot.case | pascal
metrics_endpoint = true

[cache]
enabled = true
ttl_seconds = 60

[circuit_breaker]
enabled = true
failure_threshold = 5
reset_timeout_seconds = 30
```

## Anti-patterns

```csharp
// ❌ WRONG — appsettings.json in source
// (file would exist at src/host/Tessera.Host/appsettings.json)

// ❌ WRONG — hardcoded URLs in C# code
[Get("/select/0/jaeger/api/services")]  // tenant hardcoded
Task<...> GetServicesAsync(...);

// ❌ WRONG — secrets in TOML file
token = "abc123-secret-do-not-commit"

// ❌ WRONG — missing version field
// (no `version = "1"` at top of tessera.toml)

// ❌ WRONG — options without ValidateOnStart()
builder.Services.Configure<VictoriaOptions>(builder.Configuration.GetSection("victoria"));
```

## Test requirements

- `tests/unit/core/Tessera.Shared.Unit/`:
  - `TomlConfigurationProviderTests.Loads_File`
  - `TomlConfigurationProviderTests.Paths_Order_Wins`
  - `TomlConfigurationProviderTests.Secrets_EnvResolved`
  - `TomlConfigurationProviderTests.Secrets_FileResolved`
  - `TomlConfigurationProviderTests.Version_Validation_FailsOnMismatch`
- Integration: end-to-end config load → app startup → options resolved

## Related docs

- `architecture.md` — overall architecture
- `security/auth-model.md` — admin token source
- `../rules/coding/naming-and-types.md` — sealed classes, primary constructors
- `../rules/coding/project-naming-and-setup.md` — naming convention