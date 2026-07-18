---
description: IOptions pattern — IOptions<T> for read, IOptionsSnapshot<T> for scoped reload, ValidateOnStart for fail-fast
globs: ["**/*.cs", "**/Directory.Build.props"]
always: true
---

# Dependency injection — Options pattern

## 1. `IOptions<T>` for configuration

Use `IOptions<T>` (singleton) when settings don't change at runtime:

```csharp
public sealed class GetTraceHandler(
    IVictoriaTracesClient client,
    IOptions<VictoriaOptions> options)
{
    public async Task<TraceDetail> HandleAsync(string traceId, CancellationToken ct)
    {
        var tenant = options.Value.Tenant;        // reads at call time
        var timeout = options.Value.TimeoutMs;
        return await client.GetTraceAsync(tenant, traceId, ct);
    }
}
```

**Don't capture `options.Value` in a field** — it won't reflect reload:

```csharp
// ❌ Wrong — captured at construction time
public sealed class GetTraceHandler(IOptions<VictoriaOptions> options)
{
    private readonly VictoriaOptions _opts = options.Value;  // snapshot
}

// ✅ Correct — read at call time
public sealed class GetTraceHandler(IOptions<VictoriaOptions> options)
{
    public Task DoAsync()
    {
        var tenant = options.Value.Tenant;  // always current
        // ...
    }
}
```

## 2. `IOptionsSnapshot<T>` for scoped reload

Use `IOptionsSnapshot<T>` (scoped) when you need to re-read on each request:

```csharp
public sealed class GetTraceHandler(
    IVictoriaTracesClient client,
    IOptionsSnapshot<VictoriaOptions> options)  // ← snapshot per scope
{
    public Task DoAsync()
    {
        var tenant = options.Value.Tenant;
        // ...
    }
}
```

**Caveat:** `IOptionsSnapshot<T>` is scoped — can't be injected into singletons.

## 3. `ValidateOnStart()` — fail-fast

```csharp
// src/host/Tessera.Host/Program.cs
builder.Services
    .AddOptions<VictoriaOptions>()
    .Bind(builder.Configuration.GetSection("victoria"))
    .ValidateOnStart();   // ← validates at startup, not first request

builder.Services.AddSingleton<
    IValidateOptions<VictoriaOptions>, VictoriaOptionsValidator>();
```

`ValidateOnStart()` ensures invalid config crashes the process at startup
with a clear error, not 30 minutes later when the first request comes in.

## 4. Validator pattern

```csharp
public sealed class VictoriaOptionsValidator : IValidateOptions<VictoriaOptions>
{
    public ValidateOptionsResult Validate(string? name, VictoriaOptions options)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(options.Traces.Url))
            errors.Add($"{name ?? "victoria"}.traces.url is required");
        if (string.IsNullOrWhiteSpace(options.Logs.Url))
            errors.Add($"{name ?? "victoria"}.logs.url is required");
        if (options.TimeoutMs < 100)
            errors.Add($"{name ?? "victoria"}.timeout_ms must be >= 100");
        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }
}
```

## 5. DataAnnotations validation

For simple cases, use `[Required]`, `[Range]`, etc. on options classes:

```csharp
public sealed class CacheOptions
{
    [Range(1, 3600)]
    public int TtlSeconds { get; init; } = 60;

    [Required]
    public string KeyPrefix { get; init; } = "tessera";
}

builder.Services
    .AddOptions<CacheOptions>()
    .Bind(builder.Configuration.GetSection("cache"))
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

## 6. Named options (multiple instances)

```csharp
// Registration
builder.Services
    .AddOptions<VictoriaOptions>("production")
    .Bind(builder.Configuration.GetSection("victoria:production"))
    .ValidateOnStart();

builder.Services
    .AddOptions<VictoriaOptions>("staging")
    .Bind(builder.Configuration.GetSection("victoria:staging"))
    .ValidateOnStart();

// Consumption
public sealed class MultiEnvHandler(
    IOptionsSnapshot<VictoriaOptions> productionOptions,
    IOptionsSnapshot<VictoriaOptions> stagingOptions)
{
    // productionOptions.Get("production")
    // productionOptions.Get("staging")
}
```

For MVP, single options instance. Named options for future multi-env.

## 7. `PostConfigure` — derived values

```csharp
// After binding, compute derived values
builder.Services
    .AddOptions<TraceOptions>()
    .Bind(builder.Configuration.GetSection("traces"))
    .PostConfigure(opts =>
    {
        opts.FullUrl = $"{opts.BaseUrl}/select/{opts.Tenant}/jaeger";
    });
```

## 8. Testing options

```csharp
// In tests, set options directly
var options = Options.Create(new CacheOptions { TtlSeconds = 30 });
var handler = new CacheHandler(options.Object, /* ... */);
```

## Anti-patterns

```csharp
// ❌ Wrong — direct IConfiguration injection
public sealed class Handler(IConfiguration config)
{
    var url = config["victoria:traces:url"];  // stringly-typed
}

// ❌ Wrong — captured options.Value
private readonly VictoriaOptions _opts;  // stale on reload

// ❌ Wrong — without ValidateOnStart()
builder.Services.Configure<VictoriaOptions>(...);  // fails on first request, not startup

// ❌ Wrong — mutable options
public sealed class MutableOptions
{
    public string Url { get; set; }  // not init, can change at runtime
}
```

## Self-audit grep

```bash
# Direct IConfiguration usage (should be IOptions<T>)
rg -n "IConfiguration\s+\w+" src/ --type cs

# Captured options.Value in field
rg -nB1 "private readonly.*options.Value" src/ --type cs

# Configure without ValidateOnStart (allowed in tests)
rg -n "\.Configure<" src/ --type cs | rg -v "ValidateOnStart"
```

## Related rules

- `di-lifetimes.md` — service lifetimes
- `../../docs/architecture/config-format.md` — TOML binding
- `code-shape.md` — var, braces