---
description: unit testing — xUnit + hand-written doubles, AAA pattern, naming, builders, anti-patterns
globs: ["**/*Unit*.cs", "**/*Tests.cs"]
always: true
---

# Unit testing

MVP-01 stack: **xUnit + hand-written double classes + xUnit `Assert.*`** —
no Shouldly, NSubstitute, Bogus (see `testing-stack-and-pyramid.md` for
the rationale).

## 1. Structure — AAA

```csharp
[Fact]
public async Task GetTraceHandler_ValidTraceId_ReturnsTraceDetail()
{
    // Arrange — hand-written double or a real instance
    var client = new FakeVictoriaTracesClient
    {
        TraceResponse = new TraceResponse(/* ... */),
    };
    var logger = NullLogger<GetTraceHandler>.Instance;
    var handler = new GetTraceHandler(client, logger);

    // Act
    var result = await handler.HandleAsync("abc123", CancellationToken.None);

    // Assert — xUnit Assert.* only
    Assert.NotNull(result);
    Assert.Equal("abc123", result.TraceId);
}
```

## 2. Naming — `Method_State_Expected`

```csharp
// ✅ Pattern: Method_State_Expected
public async Task GetTraceAsync_ValidId_ReturnsTrace() { }
public async Task GetTraceAsync_NetworkError_ThrowsRetried() { }
public async Task GetTraceAsync_Cancelled_ThrowsOperationCancelled() { }
public void DotCase_PascalCase_ReturnsDotCase() { }
public void DotCase_EmptyString_ReturnsEmpty() { }

// ❌ Bad names
public async Task Test1() { }
public async Task TraceTests() { }
public async Task ItWorks() { }
```

## 3. One assertion concept per test

Multiple `Assert.*` calls OK when they verify **one concept**:

```csharp
// ✅ OK — one concept: "trace was found"
Assert.NotNull(result);
Assert.Equal("abc123", result.TraceId);
Assert.NotEmpty(result.Spans);

// ❌ Bad — multiple concepts in one test
[Fact]
public async Task GetTrace_DoesEverything()
{
    // Fetches, validates, caches, logs, etc.
}
```

Split into separate `[Fact]` methods when concepts diverge.

## 4. Builders for complex objects

```csharp
public sealed class TraceDetailBuilder
{
    private string _traceId = "default-id";
    private string _rootService = "test-service";
    private long _durationMs = 100;
    private string _status = "ok";

    public TraceDetailBuilder WithTraceId(string traceId) { _traceId = traceId; return this; }
    public TraceDetailBuilder WithDuration(long ms) { _durationMs = ms; return this; }
    public TraceDetailBuilder WithError() { _status = "error"; return this; }

    public TraceDetail Build() => new()
    {
        TraceId = _traceId,
        RootService = _rootService,
        DurationMs = _durationMs,
        Status = _status,
    };
}

// Use:
var trace = new TraceDetailBuilder().WithDuration(5000).WithError().Build();
```

## 5. Test data — `[Theory]` for parameterized

```csharp
[Theory]
[InlineData("TraceId", "trace.id")]
[InlineData("DurationMs", "duration.ms")]
[InlineData("SpanCount", "span.count")]
[InlineData("HTTPStatusCode", "http.status_code")]  // already dot.case — no-op
public void DotCase_Transforms_ToExpected(string input, string expected)
{
    Assert.Equal(expected, DotCaseLogRecordProcessor.ToDotCase(input));
}
```

## 6. Exceptions

```csharp
// xUnit Assert.ThrowsAsync
await Assert.ThrowsAsync<VictoriaTracesNotFoundException>(
    () => handler.HandleAsync("notfound", CancellationToken.None));

// With message check (use xUnit `Assert.Contains`)
var ex = await Assert.ThrowsAsync<InvalidOperationException>(
    () => handler.HandleAsync("notfound", CancellationToken.None));
Assert.Contains("not configured", ex.Message);
```

## 7. Cancellation

```csharp
[Fact]
public async Task HandleAsync_Cancelled_ThrowsOperationCancelled()
{
    using var cts = new CancellationTokenSource();
    cts.Cancel();

    await Assert.ThrowsAsync<OperationCanceledException>(
        () => handler.HandleAsync("abc123", cts.Token));
}
```

## 8. Time — inject `TimeProvider`

```csharp
public sealed class CacheHandler(TimeProvider clock, ICache cache)
{
    public bool IsExpired(DateTimeOffset stored)
    {
        return clock.GetUtcNow() - stored > TimeSpan.FromMinutes(5);
    }
}

// In test:
var fakeClock = new FakeTimeProvider(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
var handler = new CacheHandler(fakeClock, /* ... */);

// Advance time
fakeClock.Advance(TimeSpan.FromMinutes(10));
```

## 9. Hand-written doubles — the canonical mocking pattern

```csharp
// MVP-01 canonical pattern: a tiny hand-written test double. Subclass
// the interface with sealed class + override only the methods the test
// needs. No reflection, no dynamic proxy, no transitive deps.
//
// Real example from Tessera.Providers.Victoria.Unit: the file-static
// helpers (VictoriaTraceMapper, VictoriaLogMapper) are `internal static`,
// so tests don't need doubles for them — they call the real code.
//
// For interfaces that DO need to be substituted (e.g. Refit clients in
// upstream module tests), use this pattern:

public sealed class FakeTraceProvider : ITraceProvider
{
    public List<TraceDetail> TraceDetails { get; init; } = [];

    public Task<TraceDetail?> GetByIdAsync(TraceId traceId, CancellationToken ct) =>
        Task.FromResult(TraceDetails.FirstOrDefault(t => t.TraceId == traceId));

    public Task<Page<TraceSummary>> SearchAsync(TraceSearchQuery q, CancellationToken ct) =>
        Task.FromResult(new Page<TraceSummary>([], null, false));
}

// Use:
var provider = new FakeTraceProvider
{
    TraceDetails = [new TraceDetail(/* ... */, /* ... */)],
};
var handler = new GetTraceHandler(provider, /* ... */);
```

**Why this and not NSubstitute**: see `testing-stack-and-pyramid.md` —
the transitive deps break on net10 with our package mix. Hand-written
doubles are static, deterministic, IDE-friendly, and require no
runtime/proxy infrastructure.

## 10. Anti-patterns

```csharp
// ❌ Multiple unrelated asserts
[Fact]
public async Task Test1()
{
    var trace = await handler.HandleAsync("abc", default);
    Assert.NotNull(trace);
    Assert.Equal("error", trace.Status);
    Assert.Equal(1, cache.SetCalls);   // unrelated
    // ... unrelated assertions
}

// ❌ Testing implementation details
[Fact]
public async Task Handler_Calls_InternalHelperMethod()
{
    await handler.HandleAsync("abc", default);
    // ❌ testing internals — would require a substitute (NSubstitute.Received()).
    // With hand-written doubles, this category of test vanishes naturally.
}

// ❌ Shared mutable state
public static class TestState
{
    public static TraceDetail SharedTrace = new();  // ❌ parallel tests interfere
}

// ❌ No assertion
[Fact]
public async Task HandleAsync_DoesntThrow()
{
    await handler.HandleAsync("abc", default);
    // no assert — passes if method returns without throwing
    // (use Assert.Null(recordedException) or convert to Assert.NoThrow on the inner task)
}
```

## 11. Test isolation — no shared state

```csharp
// ✅ Each test creates its own instances
[Fact]
public async Task Test1()
{
    var handler = new GetTraceHandler(new FakeTraceProvider(), /* ... */);
    // ...
}

[Fact]
public async Task Test2()
{
    var handler = new GetTraceHandler(new FakeTraceProvider(), /* ... */);
    // ...
}
```

## 12. Constants and `[Theory]` arrays — `static readonly` per CA1861

```csharp
// ❌ Wrong — inline array allocations (CA1861 fires)
[Theory]
[InlineData("a")]
[InlineData("b")]
[InlineData("c")]
public void Test_InlineArray(string s)
{
    foreach (var item in new[] { "a", "b", "c" })
        Assert.NotNull(item);  // CA1861 — constant array allocated per call
}

// ✅ Correct — `static readonly` shared across test invocations
private static readonly string[] Letters = ["a", "b", "c"];
```

## 13. Coverage exclusions

Exclude generated code, migrations, Program.cs:

```xml
<PropertyGroup Condition="'$(IsTestProject)' == 'true'">
  <ExcludeByFile>**/Generated/**/*.cs</ExcludeByFile>
  <ExcludeByFile>**/Migrations/**/*.cs</ExcludeByFile>
</PropertyGroup>
```

## Self-audit grep

```bash
# Shouldly / NSubstitute / Bogus usage (banned in MVP-01 tests)
rg -n "Should\.|Substitute\.For|Faker<|Bogus\." tests/ --type cs

# Tests without assertions
rg -n "\[Fact\]" tests/ --type cs -A 20 | rg -v "Assert\.|Fact\(|Theory\(|InlineData"

# Bad test names
rg -n "public (async )?Task\s+\w+\(\)" tests/ --type cs | rg -v "_" | rg -v "TestData"
```

## Related rules

- `testing-stack-and-pyramid.md` — stack, layout
- `testing-integration.md` — integration tests
- `code-shape.md` — production code style
