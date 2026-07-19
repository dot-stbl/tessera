using Microsoft.Extensions.DependencyInjection;

namespace Tessera.Modules.Discovery.DependencyInjection;

/// <summary>
///     Composition-root helper for the Discovery module. The module has no
///     module-scoped services (it consumes <see cref="Tessera.Shared.Kernel.Providers.Discovery.IDiscoveryProvider" />
///     directly), so this extension is intentionally empty — it's here so
///     <c>Program.cs</c> has a consistent <c>AddXxxModule()</c> chain across
///     all four feature modules.
/// </summary>
public static class DiscoveryModuleExtensions
{
    /// <summary>
    ///     Register Discovery-module services with the DI container.
    /// </summary>
    public static IServiceCollection AddDiscoveryModule(this IServiceCollection services)
    {
        return services;
    }
}