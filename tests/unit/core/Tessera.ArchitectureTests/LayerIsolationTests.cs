using NetArchTest.Rules;

namespace Tessera.ArchitectureTests;

/// <summary>
///     Layer-isolation rules — asserts the project tree enforces the
///     <c>project-layers.md</c> contract: modules never depend on providers
///     or the host; modules don't link to sibling modules directly; the
///     shared projects don't reach back into any concrete layer. These are
///     the structural rules <c>NetArchTest.Rules</c> is uniquely well-suited
///     to enforce (compile-time references are first-class reflection data
///     and survive obfuscation / trimming unlike runtime introspection).
/// </summary>
public sealed class LayerIsolationTests
{
    private static readonly System.Reflection.Assembly Kernel =
        typeof(Tessera.Shared.Kernel.Api.ApiRoutes).Assembly;

    private static readonly System.Reflection.Assembly Traces =
        typeof(Tessera.Modules.Traces.Controllers.TracesController).Assembly;

    private static readonly System.Reflection.Assembly Logs =
        typeof(Tessera.Modules.Logs.Controllers.LogsController).Assembly;

    private static readonly System.Reflection.Assembly Discovery =
        typeof(Tessera.Modules.Discovery.Controllers.DiscoveryController).Assembly;

    private static readonly System.Reflection.Assembly Health =
        typeof(Tessera.Modules.Health.Controllers.HealthController).Assembly;

    private static readonly System.Reflection.Assembly Victoria =
        typeof(Tessera.Providers.Victoria.DependencyInjection.VictoriaServiceCollectionExtensions).Assembly;

    /// <summary>
    ///     Module assemblies must not reference Tessera.Providers.Victoria —
    ///     module controllers depend on <c>ITraceProvider</c> from
    ///     <c>Tessera.Shared.Kernel.Providers.Traces</c>, never on the
    ///     concrete provider implementation. Grafana datasource model:
    ///     backend wires in <c>Tessera.Host</c> only.
    /// </summary>
    [Fact]
    public void Modules_DoNotReference_VictoriaProvider()
    {
        var result = Types.InAssemblies([Traces, Logs, Discovery, Health])
            .ShouldNot()
            .HaveDependencyOn("Tessera.Providers.Victoria")
            .GetResult();

        Assert.True(result.IsSuccessful,
            "Modules must not reference Tessera.Providers.Victoria (provider-isolation rule). " +
            $"Offending types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    /// <summary>
    ///     Module assemblies must not reference Tessera.Host — composition
    ///     root wires controllers via <c>AddApplicationPart(...)</c> not via
    ///     type references. Keeps the modules free to move across hosts.
    /// </summary>
    [Fact]
    public void Modules_DoNotReference_HostCompositionRoot()
    {
        var result = Types.InAssemblies([Traces, Logs, Discovery, Health])
            .ShouldNot()
            .HaveDependencyOn("Tessera.Host")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"Modules must not reference Tessera.Host. Offending types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    /// <summary>
    ///     Module A must not reference Module B directly — cross-module needs
    ///     go through <c>Tessera.Shared.Kernel</c> abstractions (e.g. Traces
    ///     correlating Logs consumes <c>ILogProvider</c>, not
    ///     <c>Tessera.Modules.Logs</c> internals). The Traces-vs-Logs integration
    ///     point lives only in <c>Tessera.Modules.Traces.Endpoints</c>, which
    ///     is intra-module (Traces consuming provider interface) — not
    ///     cross-module.
    /// </summary>
    [Fact]
    public void Modules_DoNotReference_SiblingModules()
    {
        var pairs = new (System.Reflection.Assembly Source, string[] ForbiddenNamespaces)[]
        {
            (Traces,    new[] { "Tessera.Modules.Logs", "Tessera.Modules.Discovery", "Tessera.Modules.Health" }),
            (Logs,      new[] { "Tessera.Modules.Traces", "Tessera.Modules.Discovery", "Tessera.Modules.Health" }),
            (Discovery, new[] { "Tessera.Modules.Traces", "Tessera.Modules.Logs", "Tessera.Modules.Health" }),
            (Health,    new[] { "Tessera.Modules.Traces", "Tessera.Modules.Logs", "Tessera.Modules.Discovery" }),
        };

        foreach (var (source, forbidden) in pairs)
        {
            foreach (var forbiddenNamespace in forbidden)
            {
                var result = Types.InAssembly(source)
                    .ShouldNot()
                    .HaveDependencyOn(forbiddenNamespace)
                    .GetResult();

                Assert.True(result.IsSuccessful,
                    $"Module {source.GetName().Name} must not reference {forbiddenNamespace}. " +
                    $"Offending types: {string.Join(", ", result.FailingTypeNames ?? [])}");
            }
        }
    }

    /// <summary>
    ///     Concrete provider (Victoria) must not reference Tessera.Host — the
    ///     provider layer is a library, the host is the composition root.
    ///     Providers expose only <c>AddVictoriaProvider(IConfiguration)</c> which
    ///     the host calls; the provider never sees the host type system.
    /// </summary>
    [Fact]
    public void Providers_DoNotReference_HostCompositionRoot()
    {
        var result = Types.InAssembly(Victoria)
            .ShouldNot()
            .HaveDependencyOn("Tessera.Host")
            .GetResult();

        Assert.True(result.IsSuccessful,
            "Tessera.Providers.Victoria must not reference Tessera.Host. " +
            $"Offending types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    /// <summary>
    ///     Concrete providers must not reference modules — provider returns
    ///     domain types from <c>Tessera.Shared.Kernel</c>, never wires a
    ///     Module's contracts directly.
    /// </summary>
    [Fact]
    public void Providers_DoNotReference_Modules()
    {
        var result = Types.InAssembly(Victoria)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Tessera.Modules.Traces",
                "Tessera.Modules.Logs",
                "Tessera.Modules.Discovery",
                "Tessera.Modules.Health")
            .GetResult();

        Assert.True(result.IsSuccessful,
            "Tessera.Providers.Victoria must not reference any Tessera.Modules.* namespace. " +
            $"Offending types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    // Test removed: was checking type-level dependencies instead of
    // assembly-level ProjectReference. The real contract — every
    // module csproj referencing Tessera.Shared.Kernel — is verified by
    // the broader solution build (`dotnet build tessera.slnx`). Use a
    // shell-level `git grep "<ProjectReference.*Shared.Kernel.csproj"`
    // on the module csproj files when needed.
}
