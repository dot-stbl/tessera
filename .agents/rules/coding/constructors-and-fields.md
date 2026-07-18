---
description: c# constructors and fields — primary constructor preferred, no underscore prefix, no redundant readonly
globs: ["**/*.cs"]
always: true
---

# Constructors and fields

## 1. Primary constructor — DEFAULT

For services, handlers, options, and any class with dependencies:

```csharp
// ✅ Correct — primary constructor
public sealed class GetTraceHandler(
    IVictoriaTracesClient client,
    IOptions<VictoriaOptions> options,
    ILogger<GetTraceHandler> logger)
{
    public async Task<TraceDetail> HandleAsync(string traceId, CancellationToken ct)
    {
        logger.LogInformation("Fetching trace {TraceId}", traceId);
        var tenant = options.Value.Tenant;
        return await client.GetTraceAsync(tenant, traceId, ct);
    }
}
```

## 2. No underscore-prefixed private fields

```csharp
// ❌ Wrong — redundant private readonly
public sealed class GetTraceHandler : IGetTraceHandler
{
    private readonly IVictoriaTracesClient _client;
    private readonly ILogger<GetTraceHandler> _logger;
    private readonly IOptions<VictoriaOptions> _options;

    public GetTraceHandler(
        IVictoriaTracesClient client,
        IOptions<VictoriaOptions> options,
        ILogger<GetTraceHandler> logger)
    {
        _client = client;
        _options = options;
        _logger = logger;
    }
}
```

Primary constructor covers this. The parameter **is** the field.

**Exception:** truly mutable state (rare, requires thread-safety analysis):

```csharp
// ✅ OK — mutable state requires explicit field
public sealed class RateLimiter
{
    private long _lastRefillTicks;  // mutable, not from ctor
    private long _tokens;

    public RateLimiter() { _lastRefillTicks = DateTime.UtcNow.Ticks; }
}
```

**Enforcement:** `IDE0290` (severity=warning → error).

## 3. No `ArgumentNullException.ThrowIfNull` for non-nullable params

```csharp
// ❌ Wrong — duplicates static contract
public sealed class GetTraceHandler(IVictoriaTracesClient client)
{
    public Task DoAsync()
    {
        ArgumentNullException.ThrowIfNull(client);  // client is non-nullable
        // ...
    }
}
```

Non-nullable parameter is **already** guaranteed by the compiler at the call
site. `ThrowIfNull` is redundant.

**Allowed:** `ThrowIfNullOrEmpty` / `ThrowIfNullOrWhiteSpace` for empty-string
business validation. NOT for nullable-check.

**Boundary cases (allowed):** reflection, interop, public API accepting
`object`/`dynamic` — when type system can't enforce nullability.

## 4. `required` members for required init

```csharp
public sealed record Dashboard
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string Description { get; init; } = "";
}
```

Use `required` instead of constructor for required properties when no
other init logic is needed.

## 5. Constants vs `static readonly`

```csharp
// ✅ Compile-time constant — `const`
public const int DefaultPageSize = 50;
public const string LogDateFormat = "o";

// ✅ Runtime constant — `static readonly`
public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);
public static readonly Regex IdentifierPattern = new(@"^\w+$", RegexOptions.Compiled);
```

**Rule:** `const` only for primitives and `string`. Anything else:
`static readonly`.

## 6. `init` vs `set`

```csharp
// ✅ Immutable DTO — `init`
public sealed record TraceDetail(string TraceId, long DurationMs)
{
    public string Status { get; init; } = "ok";
}

// ✅ Mutable state — `set`
public sealed class RateLimiter
{
    public long Tokens { get; set; } = 100;
}
```

**Default to `init`** for records and DTOs.

## 7. Static factory methods for complex construction

```csharp
// ✅ Static factory when construction logic exists
public sealed record TimeRange(long StartUnixMs, long EndUnixMs)
{
    public static TimeRange Last(TimeSpan span) => new(
        StartUnixMs: DateTimeOffset.UtcNow.Add(-span).ToUnixTimeMilliseconds(),
        EndUnixMs: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

    public static TimeRange FromIso(string from, string to) =>
        new(
            StartUnixMs: DateTimeOffset.Parse(from).ToUnixTimeMilliseconds(),
            EndUnixMs: DateTimeOffset.Parse(to).ToUnixTimeMilliseconds());
}
```

## Self-audit grep

```bash
# Underscore-prefixed private fields (should only appear for mutable state)
rg -n "private\s+(readonly\s+)?\w+_\w+\s*[;=]" src/ --type cs

# ArgumentNullException.ThrowIfNull (allowed only for boundary cases)
rg -n "ThrowIfNull\b" src/ --type cs

# Explicit ctor with private readonly (instead of primary)
rg -nB1 "private readonly" src/ --type cs | rg "public \w+\("
```

## Related rules

- `naming-and-types.md` — sealed, record vs class, type references
- `code-shape.md` — var, braces, ns
- `di-lifetimes.md` — DI registration patterns