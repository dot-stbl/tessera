---
description: testing stack — xUnit + NSubstitute + Shouldly + Bogus + NetArchTest + Testcontainers; pyramid: unit > integration > architecture
globs: ["**/*.csproj", "**/tests/**"]
always: true
---

# Testing stack and pyramid

## Stack

| Tool | Version | Purpose |
|------|---------|---------|
| **xUnit** | 2.9.x | Test framework |
| **NSubstitute** | 5.3.x | Mocking |
| **Shouldly** | 4.3.x | Assertions (fluent, readable) |
| **Bogus** | 35.6.x | Fake data generation |
| **NetArchTest** | 1.3.x | Architecture rules (convention enforcement) |
| **Testcontainers** | 4.8.x | Real Victoria stack in Docker for integration tests |
| **Microsoft.NET.Test.Sdk** | 17.14.x | Test SDK |
| **coverlet.collector** | 6.0.x | Code coverage |

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
 │      Unit        │  ← Mocks, in-memory, < 100ms each
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
└── integration/
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
    result.Items.ShouldHaveCount(1);
    result.Items[0].RootService.ShouldBe("checkout-api");
}
```

## Mocking — NSubstitute

```csharp
// Setup return
var client = Substitute.For<IVictoriaTracesClient>();
client.GetTraceAsync("0", "abc123", Arg.Any<CancellationToken>())
    .Returns(new TraceResponse(/* ... */));

// Setup throws
client.GetTraceAsync("0", "notfound", Arg.Any<CancellationToken>())
    .Throws(new VictoriaTracesNotFoundException("notfound"));

// Verify call
await client.Received(1).GetTraceAsync("0", "abc123", Arg.Any<CancellationToken>());
```

## Assertions — Shouldly

```csharp
// Equality
result.ShouldBe(expected);
result.ShouldNotBe(unexpected);

// Null checks
result.ShouldNotBeNull();
result.ShouldBeNull();

// Collections
items.ShouldNotBeEmpty();
items.ShouldHaveCount(5);
items.ShouldContain(x => x.Id == "abc");

// Exceptions
await Should.ThrowAsync<VictoriaTracesException>(() => handler.HandleAsync(...));

// Type checks
result.ShouldBeOfType<TraceDetail>();
result.ShouldBeAssignableTo<ITraceDetail>();
```

## Fake data — Bogus

```csharp
public sealed class TraceFaker : Faker<TraceDetail>
{
    public TraceFaker()
    {
        RuleFor(t => t.TraceId, f => f.Random.Hexadecimal(16, string.Empty));
        RuleFor(t => t.RootService, f => f.PickRandom("checkout-api", "postgres", "stripe"));
        RuleFor(t => t.DurationMs, f => f.Random.Long(10, 5000));
        RuleFor(t => t.Status, f => f.PickRandom("ok", "error"));
    }
}

// Use:
var trace = new TraceFaker().Generate();
var traces = new TraceFaker().Generate(50);
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
        result.IsSuccessful.ShouldBeTrue($"Module isolation violated: {result.GetFailingTypes()}");
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
- `testing-integration.md` — integration test specifics
- `../coding/project-deps-and-tests.md` — project layout