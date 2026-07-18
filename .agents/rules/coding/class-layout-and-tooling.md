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
8. **Private methods**

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

    // 5. Constructors (only if needed beyond primary)
    // (none — primary ctor covers it)

    // 6. Properties
    public bool IsEnabled => options.Value.Enabled;

    // 7. Public methods
    public async Task<TraceDetail?> GetOrLoadAsync(string traceId, CancellationToken ct)
    {
        // ...
    }

    // 8. Private methods
    private async Task<TraceDetail> LoadFromVictoriaAsync(string traceId, CancellationToken ct)
    {
        // ...
    }
}
```

**Enforcement:** code review (no automatic rule).

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
// TraceDetail.cs → public sealed record TraceDetail
// GetTraceHandler.cs → public sealed class GetTraceHandler
```

**Enforcement:** Roslyn `IDE0005` (single-type rule) + code review.

## 4. Required tooling

### `dotnet format`

Runs automatically as part of `dotnet build` via `VerifyFormatOnBuild` target
in `src/host/Tessera.Build.Tools/`.

```bash
dotnet format tessera.slnx --severity hidden  # canonical fix
```

### Roslynator / Meziantou / VSTHRD analyzers

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
- `../process/build-verification.md` — build gate