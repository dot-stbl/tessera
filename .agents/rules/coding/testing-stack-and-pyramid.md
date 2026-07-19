---
description: testing stack — xUnit + manual mock helpers + NetArchTest + Testcontainers; pyramid: unit > integration > architecture
globs: ["**/*Tests.cs", "**/tests/**/*.cs", "**/Tests.csproj"]
always: false
---

# Testing stack and pyramid

## Stack

| Tool | Version | Purpose |
|------|---------|---------|
| **xUnit** | 2.9.x | Test framework |
| **Microsoft.NET.Test.Sdk** | 18.8.x | Test SDK (Test Explorer, VSTest runner) |
| **NetArchTest** | 1.3.x | Architecture rules (convention enforcement — NetArchTest only, no mock/analyzer deps) |
| **Testcontainers** | 4.8.x | Real Victoria stack in Docker for integration tests |
| **coverlet.collector** | 6.0.x → 10.0.x | Code coverage |

**MVP-01 reality:** we do **not** use NSubstitute / Shouldly / Bogus.
Direct dependencies on those libraries have transitive Castle.Core /
DiffEngine / Newtonsoft.Json issues with our current package mix on
net10. Until those resolve, tests use:

- **Assertions**: plain xUnit `Assert.Equal / True / Null / Contains / Throws`
- **Mocking**: handwritten test doubles (small subclasses or
  `Substitute.For<>` IF the transitive deps are present in a given test
  project — but the *canonical* stack is xUnit-only). See
  `testing-unit.md` §11 for the patterns we use.
- **Test data**: literal constructors (no `Faker<T>`); use shared
  `static readonly` arrays for `[Theory]` rows that would otherwise
  allocate identical arrays per invocation.

When NSubstitute/Shouldly/Bogus become net10-clean, we will reintroduce
them in a follow-up rule update. The packages stay in
`Directory.Packages.props` as approved-but-unused so CI tracks them
and reviewers notice when the transitive-deps issue resolves.

## Test pyramid

```
        ┌─────┐
        │ E2E │  ← WebApplicationFactory + real Victoria (stretch)
        └─────┘
     ┌───────────┐
     │Integration│  ← Testcontainers Victoria, real HTTP
     └───────────┘
   ┌──────────────┐
   │ Architecture │  ← NetArchTest (compile-time rules)
   └──────────────┘
 ┌──────────────────┐
 │      Unit        │  ← Hand-written doubles + xUnit Assert, < 100ms each
 └──────────────────┘
```

**Coverage target:** 70% line coverage per module.

## Test project layout

```
tests/
├── unit/
│   ├── modules/                # per-module unit tests
│   │   ├── Tessera.Modules.Traces.Unit/
│   │   ├── Tessera.Modules.Logs.Unit/
│   │   └── Tessera.Modules.Dashboards.Unit/
│   └── core/                   # host, shared, architecture
│       ├── Tessera.Host.UnitTests/
│       ├── Tessera.Shared.Unit/
│       └── Tessera.ArchitectureTests/
└── integration/                # WebApplicationFactory + Testcontainers Victoria
    ├── Tessera.Host.Integration/   # WebApplicationFactory
    └── Tessera.Victoria.Integration/  # Testcontainers Victoria
```

See `../coding/project-deps-and-tests.md` for project naming.

## Test naming

```csharp
// Pattern: MethodName_StateUnderTest_ExpectedBehavior
[Fact]
public async Task GetTraceAsync_ValidId_ReturnsTraceDetail() { }

[Fact]
public async Task GetTraceAsync_VictoriaReturns404_ReturnsNotFound() { }

[Fact]
public async Task GetTraceAsync_VictoriaTimesOut_ThrowsWithRetries() { }

[Fact]
public void DotCaseLogRecordProcessor_PascalCaseKey_ConvertsToDotCase() { }
```

## Test structure — AAA

```csharp
[Fact]
public async Task ListTracesHandler_ValidRequest_ReturnsSummaries()
{
    // Arrange
    var client = Substitute.For<IVictoriaTracesClient>();
    client.SearchTracesAsync("0", "checkout-api", null, Arg.Any<long?>(), Arg.Any<long?>(),
            null, null, 50, Arg.Any<CancellationToken>())
        .Returns(new TracesResponse([/* ... */], 1));

    var options = Options.Create(new VictoriaOptions { Tenant = "0" });
    var handler = new ListTracesHandler(client, options);

    // Act
    var result = await handler.HandleAsync(
        new ListTracesRequest(Service: "checkout-api", StartUnixMs: 0, EndUnixMs: 1),
        CancellationToken.None);

    // Assert
    Assert.Single(result.Items);
    Assert.Equal("checkout-api", result.Items[0].RootService);
}
```

## Mocking — handwritten doubles (MVP-01 default)

```csharp
// MVP-01 canonical pattern: a tiny hand-written test double. Subclass
// the interface with sealed class + override only the methods the test
// needs. No reflection, no dynamic proxy, no transitive deps.
public sealed class FakeTraceProvider : ITraceProvider
{
    public List<TraceDetail> TraceDetails { get; init; } = [];

    public Task<TraceDetail?> GetByIdAsync(TraceId traceId, CancellationToken ct) =>
        Task.FromResult(TraceDetails.FirstOrDefault(t => t.TraceId == traceId));

    public Task<Page<TraceSummary>> SearchAsync(TraceSearchQuery q, CancellationToken ct) =>
        Task.FromResult(new Page<TraceSummary>([], null, false));
}

// In the test:
var provider = new FakeTraceProvider
{
    TraceDetails = [new TraceDetail(/* ... */, /* ... */)],
};
var handler = new GetTraceHandler(provider, /* ... */);
```

## Assertions — xUnit Assert.*

```csharp
// Equality
Assert.Equal(expected, result);
Assert.NotEqual(unexpected, result);

// Null checks
Assert.NotNull(result);
Assert.Null(value);

// Boolean / type
Assert.True(condition);
Assert.IsType<TraceDetail>(result);

// Collections
Assert.NotEmpty(items);
Assert.Single(items);                       // .NET 8+: was .ShouldHaveCount(1)
Assert.Contains(items, x => x.Id == "abc");

// Exceptions
await Assert.ThrowsAsync<VictoriaTracesNotFoundException>(
    () => handler.HandleAsync("notfound", CancellationToken.None));
```

## Test data — literal constructors

```csharp
// No Faker<T> (Bogus not used). Build test data inline or via builder
// classes (see testing-unit.md §4). Patterns:
//   1. Inline object init for short construction
//   2. Builder for > 3 fields
//   3. `static readonly` shared array for [Theory] rows to satisfy CA1861

public sealed class TraceDetailBuilder
{
    private string _traceId = "default-id";
    private string _rootService = "test-service";
    private long _durationMs = 100;
    private string _status = "ok";

    public TraceDetailBuilder WithTraceId(string traceId) { _traceId = traceId; return this; }
    // ... etc

    public TraceDetail Build() => new(/* ... */);
}
```

## Architecture tests — NetArchTest

```csharp
public sealed class ArchitectureTests
{
    [Fact]
    public void Modules_ShouldNotReferenceEachOtherDirectly()
    {
        var modules = Types.InAssembly(typeof(Tessera.Modules.Traces.IVictoriaTracesClient).Assembly);
        var otherModules = modules.That().ResideInNamespace("Tessera.Modules")
            .And().DoNotHaveNameStartingWith("Traces");  // self-exclude

        var result = modules.ShouldNot().HaveDependencyOnAny(otherModules).GetResult();
        Assert.True(result.IsSuccessful, $"Module isolation violated: {result.GetFailingTypes()}");
    }

    [Fact]
    public void NoFolder_ShouldHaveMoreThanFiveProjects()
    {
        // Custom — file system check, not NetArchTest pure
    }
}
```

## Integration tests — Testcontainers Victoria

```csharp
public sealed class VictoriaContainer : IAsyncLifetime
{
    private readonly GenericContainer _vtContainer = new GenericBuilder()
        .WithImage("victoriametrics/victoria-traces:v0.X.X")
        .WithPortBinding(10428, true)
        .Build();

    public string Endpoint => $"http://localhost:{_vtContainer.GetMappedPublicPort(10428)}";

    public async Task InitializeAsync()
    {
        await _vtContainer.StartAsync();
        // Wait for Victoria to be ready
        await WaitForHealthy();
    }

    public async Task DisposeAsync() => await _vtContainer.DisposeAsync();
}

public sealed class TesseraHostFixture : IAsyncLifetime
{
    public VictoriaContainer Victoria { get; } = new();
    public WebApplicationFactory<Program> WebApp { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await Victoria.InitializeAsync();
        WebApp = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(b => b.UseSetting("victoria:traces:url", Victoria.Endpoint));
    }
}
```

## Build & verify

```bash
dotnet test tests/unit/modules/Tessera.Modules.Traces.Unit/ --no-build
dotnet test tests/integration/Tessera.Host.Integration/ --no-build
```

Both must pass before commit (see `../process/build-verification.md`).

## Related rules

- `testing-unit.md` — unit test specifics
- `testing-integration.md` — integration tests
- `../coding/project-deps-and-tests.md` — project layout
