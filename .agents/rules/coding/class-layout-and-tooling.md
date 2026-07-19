---
description: c# class member layout order, file organization, required tooling (dotnet format, analyzers)
globs: ["**/*.cs"]
always: true
---

# Class layout and tooling

## 1. Member ordering — REQUIRED

Inside a class, members in this order:

1. **Static fields** (private, public)
2. **Static properties**
3. **Static factories / methods**
4. **Instance fields** (rare with primary ctor — only mutable state)
5. **Constructors** (only when primary ctor is insufficient)
6. **Properties**
7. **Public methods**

> **Private methods are BANNED** (see §1a). Helpers live in file-static
> classes or separate helper classes per the
> `folder-organization.md` 1-type-per-file rule.

```csharp
public sealed class TraceCache(
    IOptions<CacheOptions> options,
    IMemoryCache cache)
{
    // 1. Static fields
    private static readonly ActivitySource Activity = new("Tessera.Traces.Cache");

    // 2. Static properties
    public static TimeSpan DefaultTtl { get; } = TimeSpan.FromSeconds(60);

    // 3. Static factories
    public static TraceCacheKey ForTrace(string traceId) => new(traceId);

    // 5. Constructors (only when primary ctor is insufficient)
    // (none — primary ctor covers it)

    // 6. Properties
    public bool IsEnabled => options.Value.Enabled;

    // 7. Public methods
    public async Task<TraceDetail?> GetOrLoadAsync(string traceId, CancellationToken ct)
    {
        // ...delegate to a file-static helper, do NOT extract private method
    }
    // ← no "8. Private methods" section — banned, period
}
```

**Enforcement:** code review + `RCS1213` (warning) on every private method.

## 1a. NO private methods — extract everything to file-static helpers

**Hard rule.** Every `private` method in this codebase is a violation.
"Private" means encapsulated logic on a single class that no other consumer
can see — this is the procedural style, which is **explicitly banned** here.

The solution is always one of:

| Pattern | When | Where it lives |
|---------|------|----------------|
| **File-scoped `static class`** (`static class TraceMapping;`) | helper logic that consumes types from the same feature | top-level file in `Mapping/` subfolder |
| **Named static class** (`public static class VictoriaTraceMapper`) | helper logic that crosses feature boundaries or is reused | dedicated file in `Mapping/` |
| **Extension method class** (`public static class SpanIdExtensions`) | adds behavior to an existing type without modification | dedicated file in `Extensions/` |
| **Standalone helper class** with instances | when state matters (rare) | regular file in `Helpers/` |

```csharp
// ❌ Wrong — procedural, encapsulated logic
public sealed class VictoriaTraceProvider(IVictoriaTracesClient client, VictoriaOptions options)
{
    public async Task<TraceDetail?> GetByIdAsync(...) { ... }
    private static DomainSpan MapToDomainSpan(...) { ... }     // BANNED
    private static TraceStatus MapStatus(...) { ... }          // BANNED
    private static JaegerSpan? FindRootSpan(...) { ... }       // BANNED
}

// ✅ Right — no private members, all logic is delegated to file-static helpers
public sealed class VictoriaTraceProvider(IVictoriaTracesClient client, VictoriaOptions options)
{
    public async Task<TraceDetail?> GetByIdAsync(TraceId traceId, CancellationToken ct)
    {
        var response = await client.GetTraceAsync(options.Tenant, traceId.Value, ct);
        return response.Data.Count == 0
            ? null
            : VictoriaTraceMapper.ToTraceDetail(response.Data[0]);
    }

    public async Task<Page<TraceSummary>> SearchAsync(TraceSearchQuery query, CancellationToken ct)
    {
        var response = await client.SearchTracesAsync(...);
        return VictoriaTraceMapper.ToTraceSummariesPage(response.Data);
    }
}

// File: Implementation/Mapping/VictoriaTraceMapper.cs
public static class VictoriaTraceMapper
{
    public static TraceDetail ToTraceDetail(JaegerTrace jaeger) { ... }
    public static Page<TraceSummary> ToTraceSummariesPage(IReadOnlyList<JaegerTrace> traces) { ... }

    // Internal helpers that don't need to be exposed publicly:
    internal static DomainSpan ToDomainSpan(JaegerSpan dto, IReadOnlyDictionary<string, JaegerProcess> processes) { ... }
    internal static TraceStatus ToStatus(JaegerSpan span) { ... }
    internal static JaegerSpan? FindRootSpan(JaegerTrace trace) { ... }
}
```

The helpers above are `internal static`, not `private static` — `internal`
puts them at file-level visibility (not class-bound), and `static` makes them
pure functions with no instance state.

**Why this rule exists:**

- **Single Responsibility.** A class with private methods is doing more
  than one thing. Extraction forces the question "is this really part of
  X or should it be its own thing?".
- **Testability.** A `private static` method is untestable in isolation
  (you have to test it through the class). A file-static `internal static`
  method can be tested directly.
- **Refactoring surface.** Private methods are invisible to the rest of
  the codebase. When the same logic appears in three places, the
  refactoring incentive is zero because the surface is "buried". File-static
  helpers make duplication visible.
- **SOLID.** A class full of private helpers is a "God Object" in waiting.
  - S — Single responsibility: helpers belong to OTHER responsibilities.
  - O — Open/closed: closed for extension because logic is hidden.
  - L — Liskov: irrelevant here but extraction helps.
  - I — Interface segregation: helpers should be optional dependencies.
  - D — Dependency inversion: helpers should be injected, not hard-bound.

**Allowed private members** (framework-required only):

```csharp
// These are framework-mandated overrides, not "private methods as helper"
// — they cannot reasonably be extracted to file-static helpers.

private bool Equals(MyType other) => ...;          // IEquatable override
private bool DisposedField { get; set; }          // IDisposable state
public sealed class MyType : IDisposable
{
    private void Dispose(bool disposing) { ... }    // IDisposable pattern
}

// Minimal API endpoint handler (see api-design.md §1) — referenced as
// a method group from MapGet/MapPost/MapDelete; `private static` keeps
// the handler off the public API surface (consumers must not call
// endpoint handlers directly — only via HTTP).
public static class TracesEndpoint
{
    public static void MapTracesEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGroup("/api/traces").MapGet("/{id}", GetTraceAsync);
    }

    private static async Task<...> GetTraceAsync(...) { ... }
}
```

These are **exemptions**, not encouraged. If you find yourself adding
"framework-required" exemptions liberally, reconsider the design.

**Crucial:** even inside endpoint handlers, DTO mappings (e.g.
`ToTraceSummary`, `ToTraceDetail`) must delegate to a separate
file-scoped static class — they cannot be `private static` helpers
in the same file. See `api-design.md` §1 for the canonical pattern.

**Enforcement:** every PR review checks for `private ` (followed by
method-like keyword) outside of the above exemptions. `RCS1213`
(Warn when unused private member) is left at default severity to catch
this category of violation.

## 2. One type per file — REQUIRED

```csharp
// ❌ Wrong
// Trace.cs
public sealed class Trace { }
public sealed class Span { }

// ✅ Correct
// Trace.cs
public sealed class Trace { }

// Span.cs
public sealed class Span { }
```

**Exception:** nested types (e.g. `DashboardVersionEntity` nested in
`Dashboard` is OK if logically tied).

## 3. File name matches type name

```csharp
// Trace.cs → public sealed class Trace
// GetTraceHandler.cs → public sealed class GetTraceHandler
// VictoriaOptions.cs → public sealed class VictoriaOptions
// VictoriaTraceMapper.cs → public static class VictoriaTraceMapper
```

**Enforcement:** Roslynator convention (file name must match the
first public type's name); code review.

## 4. Required tooling

### `dotnet format`

Runs automatically as part of `dotnet build` via `VerifyFormatOnBuild` target
in `src/host/Tessera.Build.Tools/`.

```bash
dotnet format tessera.slnx --severity hidden  # canonical fix
```

### Roslynator / VSTHRD / CA analyzers

Enabled globally via `Directory.Build.props`. See `analyzers.md`.

### IDE setup (recommendation)

- **Rider** or **VS 2022 17.10+** with C# dev kit
- Install ReSharper or use built-in Rider inspections
- Set `dotnet_format_on_save = true`

## 5. No nullable warnings, no analyzer warnings

Build fails on any warning (`TreatWarningsAsErrors=true`). Don't suppress
without rationale (see `analyzers.md`).

## 6. Assembly-level attributes

```csharp
[assembly: CLSCompliant(false)]  // we don't claim CLS compliance
```

Only when meaningful. Don't add by default.

## 7. File headers

Plexor doesn't require file headers. Tessera doesn't either. Don't add
`// Copyright (c) ...` boilerplate.

## Related rules

- `code-shape.md` — var, braces, file-scoped namespaces
- `constructors-and-fields.md` — primary ctor
- `naming-and-types.md` — type naming
- `analyzers.md` — analyzer configuration
- `folder-organization.md` — 1 type per file (helpers go in their own files)
- `../process/build-verification.md` — build gate
