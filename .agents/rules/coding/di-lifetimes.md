---
description: DI lifetimes — Singleton/Scoped/Transient, captive dependency anti-pattern, HttpClient lifetime, AsyncLocal safety
globs: ["**/*.cs"]
always: true
---

# Dependency injection — Service lifetimes

## 1. Three lifetimes

| Lifetime | Created | Lifetime | Use for |
|----------|---------|----------|---------|
| `Singleton` | Once per app | App lifetime | Stateless services, caches, options, Refit clients |
| `Scoped` | Once per request | HTTP request | DB contexts, request-scoped state |
| `Transient` | Each injection | Until disposed | Lightweight, stateless utilities |

**Default:** start with `Scoped`. Use `Singleton` only when provably
thread-safe.

## 2. Refit clients — Singleton

Refit clients are thread-safe and expensive to create (build the typed
interface). Always register as Singleton with `HttpClient`:

```csharp
// src/shared/Tessera.Shared.Http/RefitExtensions.cs
public static class RefitExtensions
{
    public static IHttpClientBuilder AddVictoriaTracesClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var section = configuration.GetSection("victoria:traces");
        var url = section["url"]
            ?? throw new InvalidOperationException("victoria.traces.url required");

        return services
            .AddRefitClient<IVictoriaTracesClient>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(url))
            .AddHttpMessageHandler<BearerTokenHandler>()
            .AddStandardResilienceHandler();
    }
}
```

## 3. Handlers — Singleton or Transient

Handlers in tessera are stateless (DI'd dependencies are singleton/scoped).

```csharp
// Singleton — preferred for performance
builder.Services.AddSingleton<GetTraceHandler>();

// Transient — if handler is heavy or has request-scoped state
builder.Services.AddTransient<GetTraceHandler>();
```

For minimal API endpoints with `[FromServices]`, the per-request injection
makes lifetime mostly irrelevant. Choose based on instantiation cost.

## 4. Captive dependency — BANNED

```csharp
// ❌ Wrong — Scoped service captured by Singleton
builder.Services.AddSingleton<MyCache>();
builder.Services.AddScoped<DbContext>();

public sealed class MyCache(DbContext db)  // ❌ Scoped captured by Singleton
{
    // DbContext is scoped to a request, but MyCache is singleton — broken
}

// ✅ Correct — match lifetimes or use factory
public sealed class MyCache(IServiceProvider sp)
{
    public async Task<T> GetAsync<T>(string key)
    {
        await using var scope = sp.CreateAsyncScope();  // fresh scope per call
        var db = scope.ServiceProvider.GetRequiredService<DbContext>();
        // ...
    }
}
```

**Enforcement:** `VSTHRD012` (use `Microsoft.Extensions.DependencyInjection`).

## 5. `IServiceProvider` in singletons

```csharp
// ✅ Correct pattern — factory for scoped services
public sealed class ReportGenerator(IServiceProvider sp)
{
    public async Task<Report> GenerateAsync(string id, CancellationToken ct)
    {
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TesseraDbContext>();
        var report = await db.Reports.FindAsync(id, ct);
        return report;
    }
}
```

**Rule:** Singleton services that need scoped dependencies must use
`IServiceProvider.CreateAsyncScope()` per call.

## 6. `HttpClient` lifetime — use `IHttpClientFactory`

```csharp
// ✅ Correct — IHttpClientFactory via Refit
services.AddRefitClient<IVictoriaTracesClient>()
    .ConfigureHttpClient(...);

// ❌ Wrong — manual HttpClient instances (socket exhaustion)
public sealed class MyService
{
    private readonly HttpClient _client = new();  // long-lived, DNS issues
}
```

`HttpClient` instances held longer than ~2 minutes suffer from DNS caching
issues. Always use `IHttpClientFactory` (or Refit which uses it).

## 7. `IOptionsSnapshot<T>` in singletons — BANNED

```csharp
// ❌ Wrong — IOptionsSnapshot is scoped, can't be in singleton
public sealed class MySingletonService(IOptionsSnapshot<MyOptions> options)
{
    // Throws at runtime: "Cannot consume scoped service from singleton"
}

// ✅ Use IOptions<T> instead (singleton-safe)
public sealed class MySingletonService(IOptions<MyOptions> options)
{
    // ...
}
```

## 8. `IHostedService` — Singleton

```csharp
public sealed class TraceRetentionWorker : BackgroundService
{
    // Always singleton — hosted services run for app lifetime
}

// Registration:
builder.Services.AddHostedService<TraceRetentionWorker>();
```

## 9. Options — Singleton

`IOptions<T>` is registered as Singleton by default.

## 10. Logger — Singleton (via `ILogger<T>`)

```csharp
public sealed class Handler(ILogger<Handler> logger) { }
// ILogger<T> is registered as Singleton — safe to inject anywhere
```

## Standard tessera registrations

```csharp
// In Tessera.Host Program.cs
builder.Services.AddSingleton(GetVictoriaOptions);
builder.Services.AddRefitClient<IVictoriaTracesClient>().ConfigureHttpClient(...);
builder.Services.AddRefitClient<IVictoriaLogsClient>().ConfigureHttpClient(...);
builder.Services.AddRefitClient<IVictoriaMetricsClient>().ConfigureHttpClient(...);

builder.Services.AddSingleton<GetTraceHandler>();
builder.Services.AddSingleton<ListLogsHandler>();
builder.Services.AddSingleton<GetServicesHandler>();
builder.Services.AddSingleton<DashboardLoader>();

builder.Services.AddDbContext<TesseraDbContext>(opts =>
    opts.UseSqlite($"Data Source={storage.DatabasePath}"));
```

## Self-audit grep

```bash
# IOptionsSnapshot in singleton
rg -n "IOptionsSnapshot" src/ --type cs

# HttpClient fields (manual, not factory)
rg -n "HttpClient\s+\w+\s*=" src/ --type cs

# AsyncLocal / ThreadStatic in services (usually wrong)
rg -n "(AsyncLocal|ThreadStatic)" src/ --type cs
```

## Related rules

- `di-options.md` — IOptions pattern
- `async-and-tasks.md` — async safety
- `code-shape.md` — var, braces