---
description: unit testing — xUnit + NSubstitute + Shouldly, AAA pattern, naming, builders, anti-patterns
globs: ["**/*Unit*.cs", "**/*Tests.cs"]
always: true
---

# Unit testing

## 1. Structure — AAA

```csharp
[Fact]
public async Task GetTraceHandler_ValidTraceId_ReturnsTraceDetail()
{
    // Arrange
    var client = Substitute.For<IVictoriaTracesClient>();
    var logger = NullLogger<GetTraceHandler>.Instance;
    client.GetTraceAsync("0", "abc123", Arg.Any<CancellationToken>())
        .Returns(new TraceResponse(/* ... */));

    var handler = new GetTraceHandler(client, logger);

    // Act
    var result = await handler.HandleAsync("abc123", CancellationToken.None);

    // Assert
    result.ShouldNotBeNull();
    result.TraceId.ShouldBe("abc123");
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

Multiple `Should*` calls OK if they verify **one concept**:

```csharp
// ✅ OK — one concept: "trace was found"
result.ShouldNotBeNull();
result.TraceId.ShouldBe("abc123");
result.Spans.ShouldNotBeEmpty();

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
        Status = _status
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
    DotCaseLogRecordProcessor.ToDotCase(input).ShouldBe(expected);
}
```

## 6. Exceptions

```csharp
// Shouldly style
await Should.ThrowAsync<VictoriaTracesNotFoundException>(
    () => handler.HandleAsync("notfound", CancellationToken.None));

// With message check
await Should.ThrowAsync<InvalidOperationException>(
    () => handler.HandleAsync("notfound", CancellationToken.None))
    .Message.ShouldContain("not configured");
```

## 7. Cancellation

```csharp
[Fact]
public async Task HandleAsync_Cancelled_ThrowsOperationCancelled()
{
    using var cts = new CancellationTokenSource();
    cts.Cancel();

    await Should.ThrowAsync<OperationCanceledException>(
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

## 9. Anti-patterns

```csharp
// ❌ Multiple unrelated asserts
[Fact]
public async Task Test1()
{
    var trace = await handler.HandleAsync("abc", default);
    trace.ShouldNotBeNull();
    trace.Status.ShouldBe("error");
    cache.Received(1).Set("abc", trace);     // unrelated
    logger.Received().LogInformation("...");  // unrelated
}

// ❌ Testing implementation details
[Fact]
public async Task Handler_Calls_InternalHelperMethod()
{
    await handler.HandleAsync("abc", default);
    handler.Received().InternalHelperMethod();  // ❌ testing internals
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
    // (use Should.NotThrowAsync for explicit intent)
}
```

## 10. Test isolation — no shared state

```csharp
// ✅ Each test creates its own instances
[Fact]
public async Task Test1()
{
    var handler = new GetTraceHandler(Substitute.For<IVictoriaTracesClient>(), /* ... */);
    // ...
}

[Fact]
public async Task Test2()
{
    var handler = new GetTraceHandler(Substitute.For<IVictoriaTracesClient>(), /* ... */);
    // ...
}
```

## 11. Coverage exclusions

Exclude generated code, migrations, Program.cs:

```xml
<PropertyGroup Condition="'$(IsTestProject)' == 'true'">
  <ExcludeByFile>**/Generated/**/*.cs</ExcludeByFile>
  <ExcludeByFile>**/Migrations/**/*.cs</ExcludeByFile>
</PropertyGroup>
```

## Self-audit grep

```bash
# Tests without assertions
rg -n "\[Fact\]" tests/ --type cs -A 20 | rg -v "Should|Assert\."

# Bad test names
rg -n "public (async )?Task\s+\w+\(\)" tests/ --type cs | rg -v "_" | rg -v "TestData"
```

## Related rules

- `testing-stack-and-pyramid.md` — stack, layout
- `testing-integration.md` — integration tests
- `code-shape.md` — production code style