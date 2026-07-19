---
description: c# anti-patterns — tuple ban in public API, record vs class for DTOs, enum anti-patterns, ArgumentNullException.ThrowIfNull ban, validation
globs: ["**/*.cs"]
always: true
---

# Anti-patterns

Common C# gotchas that fail code review. Build gate catches some, others
require manual audit (see `../process/worker-audit.md`).

## 1. Tuples in public API — BANNED

```csharp
// ❌ Wrong — tuples in return types / parameters
public (TraceDetail, List<LogEntry>) GetTraceWithLogsAsync(string id, CancellationToken ct);
public void SetCoordinates((double Lat, double Lng) coords);

// ✅ Correct — record with named fields
public sealed record TraceWithLogs(TraceDetail Trace, IReadOnlyList<LogEntry> Logs);

public async Task<TraceWithLogs> GetTraceWithLogsAsync(string id, CancellationToken ct);
```

**Exception:** `var (a, b) =` destructuring on the call site of a top-level / file-static helper. Note: `private` methods themselves are banned (see `class-layout-and-tooling.md` §1a).

**Why:** tuples have no XML doc support, no extension methods, can't be
mocked, no IntelliSense for field names in some IDEs.

## 2. Records vs class for DTOs

```csharp
// ✅ Record for immutable, value-equality
public sealed record TimeRange(long StartUnixMs, long EndUnixMs);

// ✅ Class for mutable or required-with-defaults DTOs
public sealed class TraceDetail
{
    public required string TraceId { get; init; }
    public required long DurationMs { get; init; }
    public string Status { get; init; } = "ok";
}
```

**Default:** `sealed class` with `required` init properties. Use `record`
only for value types / value-equality.

## 3. Enum anti-patterns — no implicit data

```csharp
// ❌ Wrong — enum carries data
public enum CachePeriod
{
    OneMinute,
    OneHour,
    OneDay,
}

// ✅ Correct — explicit, no magic
public sealed record CachePolicy(TimeSpan Ttl, int MaxEntries);
```

**Also avoid:** large enums with switch statements sprawling across multiple
methods. Replace with polymorphism (strategy pattern, visitor) when logic
diverges.

## 4. `ArgumentNullException.ThrowIfNull` — BANNED for nullable params

```csharp
// ❌ Wrong — duplicates static contract
public sealed class Handler(IClient client)
{
    public Task DoAsync()
    {
        ArgumentNullException.ThrowIfNull(client);  // client is non-nullable
        // ...
    }
}
```

Non-nullable parameter is guaranteed by compiler. `ThrowIfNull` is redundant.

**Allowed:**
- `ThrowIfNullOrEmpty` / `ThrowIfNullOrWhiteSpace` (business validation)
- Boundary cases (reflection, interop, `object`/`dynamic`)

## 5. Validation — FluentValidation only

```csharp
// ❌ Wrong — manual validation in handler
public async Task<Results<Ok, BadRequest<ProblemDetails>>> CreateDashboardAsync(Dashboard d)
{
    if (string.IsNullOrEmpty(d.Id)) return TypedResults.BadRequest(...);
    if (d.Panels.Count == 0) return TypedResults.BadRequest(...);
    // ...
}

// ✅ Correct — FluentValidation
public sealed class CreateDashboardRequestValidator : AbstractValidator<CreateDashboardRequest>
{
    public CreateDashboardRequestValidator()
    {
        RuleFor(x => x.Id).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Panels).NotEmpty();
    }
}
```

Validators register via `services.AddValidatorsFromAssembly<T>();` in
`Tessera.Host`.

## 6. `[FromServices]` for endpoint-specific dependencies

```csharp
// ❌ Wrong — endpoint-specific service in primary ctor
public sealed class GetTraceHandler(IVictoriaTracesClient client, IClock clock, IMetrics metrics)
{
    // ... but only `client` is used by HandleAsync, `clock` and `metrics` are for one method
}

// ✅ Correct — endpoint-specific via [FromServices]
public static async Task<Results<Ok<TraceDetail>, NotFound>> GetTraceAsync(
    string id,
    IVictoriaTracesClient client,            // always needed
    [FromServices] IMetrics metrics,         // only this endpoint
    CancellationToken ct)
{
    metrics.Increment("trace.get");
    // ...
}
```

Use `[FromServices]` for dependencies needed by ONE endpoint only, not the
whole handler class.

## 7. No inline `record` in controllers / endpoints

```csharp
// ❌ Wrong — record defined inline
public static class TracesEndpoint
{
    public sealed record ListTracesRequest(string Service, long Start, long End);  // NO
    // ...
}

// ✅ Correct — separate file: ListTracesRequest.cs
public sealed record ListTracesRequest(string Service, long Start, long End);
```

## 8. `JsonSerializerOptions` — single instance, cached

```csharp
// ❌ Wrong — new options per call (slow, allocates)
var json = JsonSerializer.Serialize(obj, new JsonSerializerOptions { ... });

// ✅ Correct — cached static
public static class JsonOpts
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter() },
    };
}

var json = JsonSerializer.Serialize(obj, JsonOpts.Default);
```

## 9. No magic strings — use constants or options

```csharp
// ❌ Wrong — magic strings throughout code
if (log.Level == "ERROR") { /* ... */ }
client.GetAsync("/select/0/jaeger/api/services", ct);

// ✅ Correct — constants or options
private const string ErrorLevel = "ERROR";

[Get("/select/{tenant}/jaeger/api/services")]
Task<...> GetServicesAsync(string tenant, CancellationToken ct);
```

## 10. Avoid `dynamic` — use `object` + cast or generic

```csharp
// ❌ Wrong — dynamic loses type safety
dynamic result = client.GetSomething();
result.ProcessIt("arg");

// ✅ Correct — typed return
var result = await client.GetSomethingAsync();
result.ProcessIt("arg");
```

## Self-audit grep

```bash
# Tuples in public API
rg -n "public\s+\([^)]+\)\s+\w+\(" src/ --type cs

# async void (banned)
rg -n "async\s+void\b" src/ --type cs

# ArgumentNullException.ThrowIfNull on non-nullable
rg -nB1 "ThrowIfNull" src/ --type cs | rg -v "ThrowIfNullOr"

# Inline records in endpoint files
rg -n "^public (sealed )?record " src/ --type cs | rg -v "Models/|Handlers/|Endpoints/"

# magic string literals
rg -n '"ERROR"|"INFO"|"/select/' src/ --type cs
```

## Related rules

- `naming-and-types.md` — naming, sealed, record vs class
- `code-shape.md` — var, braces
- `api-design.md` — minimal API patterns
- `../process/worker-audit.md` — self-audit gate catches these