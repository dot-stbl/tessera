using Tessera.Stack.Testing.Fixtures;

namespace Tessera.Stack.Integration.Collections;

/// <summary>
///     xUnit <see cref="Xunit.CollectionDefinitionAttribute" /> for the
///     shared <see cref="TesseraStackFixture" /> across all
///     integration tests. Tests reference this collection via
///     <see cref="CollectionAttribute" /> on the test class so the
///     fixture is instantiated exactly once per test run.
/// </summary>
/// <remarks>
///     <b>Why shared, not per-class.</b> The
///     <see cref="TesseraStackFixture" /> wraps a real
///     <c>WebApplicationFactory&lt;TEntryPoint&gt;</c> for the
///     <c>Tessera.Host</c> process. Starting the in-memory host costs
///     ~1 second; sharing across the four feature-folder test classes
///     shaves that off every test run. The
///     <see cref="DockerComposeFixture" /> has the same argument —
///     <c>docker compose up -d --wait</c> is a multi-second hit that
///     would otherwise be paid four times.
/// </remarks>
[CollectionDefinition(Name)]
public sealed class StackCollection : ICollectionFixture<TesseraStackFixture>
{
    /// <summary>Stable name referenced by <see cref="CollectionAttribute" /> on test classes.</summary>
    public const string Name = "tessera.stack";

    /// <summary>Marker that xUnit lists in the <c>--list-tests</c> output even though the fixture is empty.</summary>
    private sealed class TesseraStackLifecycle;
}
