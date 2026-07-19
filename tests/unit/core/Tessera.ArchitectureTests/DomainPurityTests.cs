using NetArchTest.Rules;
using Xunit;

namespace Tessera.ArchitectureTests;

/// <summary>
///     Domain-purity rules — enforces the Kernel/Shared core has zero
///     framework/IO dependencies per <c>architecture.md</c> §1 (the 6 laws).
///     <see cref="Tessera.Shared.Kernel" /> is the innermost leaf — it owns
///     domain types and provider interfaces, no ASP.NET Core, no EF Core, no
///     HttpClient. Other shared projects (<c>Http</c>, <c>Web</c>,
///     <c>Authentication</c>) add framework concerns; Kernel stays pure.
/// </summary>
public sealed class DomainPurityTests
{
    private static readonly System.Reflection.Assembly Kernel =
        typeof(Tessera.Shared.Kernel.Api.ApiRoutes).Assembly;

    /// <summary>
    ///     <c>Tessera.Shared.Kernel</c> must not reference ASP.NET Core MVC
    ///     primitives — Controllers / ControllerBase / ApiController attribute
    ///     live in module projects. Kernel exposes only domain types
    ///     (<c>Trace</c>, <c>Span</c>, <c>LogEntry</c>, <c>Result&lt;T&gt;</c>,
    ///     <c>ProviderException</c>) and provider interfaces.
    /// </summary>
    [Fact]
    public void SharedKernel_DoesNotReference_AspNetCoreMvc()
    {
        var result = Types.InAssembly(Kernel)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Microsoft.AspNetCore.Mvc",
                "Microsoft.AspNetCore.Mvc.Core",
                "Microsoft.AspNetCore.Mvc.ApiExplorer")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"Tessera.Shared.Kernel must remain framework-free (no ASP.NET Core MVC). " +
            $"Offending types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    /// <summary>
    ///     <c>Tessera.Shared.Kernel</c> must not reference Entity Framework
    ///     Core. If EF appears in a model later, it goes in a new
    ///     <c>Tessera.Shared.Persistence</c> project under shared/.
    /// </summary>
    [Fact]
    public void SharedKernel_DoesNotReference_EntityFrameworkCore()
    {
        var result = Types.InAssembly(Kernel)
            .ShouldNot()
            .HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"Tessera.Shared.Kernel must remain ORM-free. " +
            $"Offending types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    /// <summary>
    ///     <c>Tessera.Shared.Kernel</c> must not reference HTTP-client SDKs.
    ///     <c>Tessera.Shared.Http</c> owns Refit/Polly/bearer handler; Kernel
    ///     defines only provider interfaces.
    /// </summary>
    [Fact]
    public void SharedKernel_DoesNotReference_HttpClientSdk()
    {
        var result = Types.InAssembly(Kernel)
            .ShouldNot()
            .HaveDependencyOnAny(
                "System.Net.Http",
                "Refit",
                "Microsoft.Extensions.Http")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"Tessera.Shared.Kernel must remain HTTP-client-free (Refit + HttpClient live in Tessera.Shared.Http). " +
            $"Offending types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    /// <summary>
    ///     Sanity check — Kernel MUST reference its own provider-interfaces
    ///     namespace (the whole point of MVP-01 is provider abstraction for
    ///     the Grafana datasource model). Catches a future PR that
    ///     accidentally renames <c>Tessera.Shared.Kernel.Providers</c>.
    /// </summary>
    [Fact]
    public void SharedKernel_OwnsProviderInterfacesNamespace()
    {
        var providerInterfaceTypes = Types.InAssembly(Kernel)
            .That()
            .ResideInNamespace("Tessera.Shared.Kernel.Providers")
            .And()
            .AreInterfaces()
            .GetTypes()
            .ToHashSet();

        // MVP-01 ships ITraceProvider + ILogProvider + IDiscoveryProvider + IHealthProvider
        Assert.Contains(providerInterfaceTypes, t => t.Name == "ITraceProvider");
        Assert.Contains(providerInterfaceTypes, t => t.Name == "ILogProvider");
        Assert.Contains(providerInterfaceTypes, t => t.Name == "IDiscoveryProvider");
        Assert.Contains(providerInterfaceTypes, t => t.Name == "IHealthProvider");
    }
}
