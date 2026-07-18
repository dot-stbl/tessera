---
description: c# code shape — var, braces, file-scoped namespaces, XML docs, pattern matching, one class per file
globs: ["**/*.cs"]
always: true
---

# Code shape

Universal C# code conventions. Builds on `.editorconfig` severity settings
(see `analyzers.md`).

## 1. `var` — always when type is obvious

```csharp
// ✅ Correct
var clients = new List<IVictoriaTracesClient>();
var trace = await client.GetTraceAsync(id, ct);
var sb = new StringBuilder(key.Length + 4);

// ❌ Wrong
List<IVictoriaTracesClient> clients = new();
TraceDetail trace = await client.GetTraceAsync(id, ct);
```

**Exceptions (explicit type):**
- `new` with collection initializer where type isn't obvious from RHS
- Numeric literals where type matters: `float f = 1.0f;`
- When var hurts readability: `var handler = factory.Create<TraceHandler>();`

**Enforcement:** `IDE0007` (severity=error), `IDE0008` = none (inverse).

## 2. File-scoped namespaces — REQUIRED

```csharp
// ✅ Correct
namespace Tessera.Modules.Traces;

public sealed class GetTraceHandler { ... }

// ❌ Wrong
namespace Tessera.Modules.Traces
{
    public sealed class GetTraceHandler { ... }
}
```

**Enforcement:** `IDE0161` (severity=error).

## 3. Braces — always

```csharp
// ✅ Correct
if (condition)
{
    DoSomething();
}

// ❌ Wrong — single-line if without braces
if (condition) DoSomething();
```

**Enforcement:** `IDE0011` (severity=error).

## 4. XML docs on public API

```csharp
/// <summary>
/// Fetches a single trace by ID and reconstructs the span tree.
/// </summary>
/// <param name="traceId">The trace identifier (32-char hex).</param>
/// <param name="ct">Cancellation token.</param>
/// <returns>The full trace detail with parent/child relationships.</returns>
public async Task<TraceDetail> HandleAsync(string traceId, CancellationToken ct)
{
    // ...
}
```

**Required on:** public methods, public classes/records, public properties.

**Enforcement:** `CS1591` (severity=warning → error via TreatWarningsAsErrors).

## 5. Pattern matching — prefer over casts

```csharp
// ✅ Correct — pattern matching
if (response is not null and { Spans.Count: > 0 })
{
    // ...
}

// ❌ Wrong — explicit cast
var spans = ((TraceResponse)response).Spans;
if (response != null && response.Spans.Count > 0)
{
    // ...
}
```

**Enforcement:** Style rules + `IDE0019`, `IDE0260`.

## 6. No `#region` directives

```csharp
// ❌ FORBIDDEN — прячет структуру, поощряет большие файлы
#region Constructors
public GetTraceHandler(...) { }
#endregion
```

If a class needs regions, **split it into multiple files / types**.

**Enforcement:** code review.

## 7. One public type per file

```csharp
// ❌ Wrong — multiple types in Trace.cs
public sealed class Trace { }
public sealed class Span { }    // separate file: Span.cs
```

Exception: nested types.

## 8. `throw` — not `throw ex`

```csharp
// ✅ Correct — preserves stack trace
catch (HttpRequestException ex)
{
    logger.LogError(ex, "Failed to fetch trace");
    throw;
}

// ❌ Wrong — resets stack trace
catch (HttpRequestException ex)
{
    logger.LogError(ex, "Failed to fetch trace");
    throw ex;
}
```

## 9. Collection initializers

```csharp
// ✅ Preferred — collection expression (C# 12+)
public IReadOnlyList<string> Tags { get; init; } = [];

// ✅ OK — explicit when intent matters
public IReadOnlyList<string> Tags { get; init; } = ["apm", "default"];

// ❌ Avoid — verbose
public IReadOnlyList<string> Tags { get; init; } = new List<string> { "apm", "default" };
```

**Enforcement:** `IDE0305` (severity=none by default — owner-disabled per plexor convention).

## 10. String interpolation over concatenation

```csharp
// ✅ Correct
var message = $"Trace {traceId} not found";

// ❌ Wrong
var message = "Trace " + traceId + " not found";
```

**Enforcement:** Style.

## 11. Expression-bodied vs block-bodied

```csharp
// ✅ Expression-bodied for trivial one-liners
public string FullName => $"{FirstName} {LastName}";
public Task<User?> GetUserAsync(string id) => repository.FindAsync(id);

// ✅ Block-bodied for non-trivial
public async Task<TraceDetail> HandleAsync(string traceId, CancellationToken ct)
{
    var response = await client.GetTraceAsync(traceId, ct);
    var tree = ReconstructSpanTree(response);
    activity?.SetTag("vt.spans.count", tree.Spans.Count);
    return tree;
}
```

**Enforcement:** `IDE0022` (severity=warning → error).

## 12. Comments — XML docs > inline

```csharp
// ✅ XML docs for public API (required)
// ✅ Inline comments for non-obvious logic, not "what" but "why"
// ❌ Comments that re-state code
var x = x + 1;  // increment x  ← useless
```

## Self-audit grep

```bash
# Non-file-scoped namespace
rg -n "^namespace\s+\w+\s*\{" src/ --type cs

# throw ex (vs throw)
rg -n "throw\s+\w+;" src/ --type cs

# Single-line if without braces
rg -n "if\s*\([^)]+\)\s+[^{]+;" src/ --type cs
```

## Related rules

- `naming-and-types.md` — naming, sealed, record vs class
- `constructors-and-fields.md` — primary ctor pattern
- `async-and-tasks.md` — async patterns
- `analyzers.md` — editorconfig severity map