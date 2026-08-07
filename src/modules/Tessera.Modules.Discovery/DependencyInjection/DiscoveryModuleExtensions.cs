using Microsoft.Extensions.DependencyInjection;
using Tessera.Modules.Discovery.Services;

namespace Tessera.Modules.Discovery.DependencyInjection;

/// <summary>
///     Composition-root helper for the Discovery module. Registers service RED
///     orchestration; provider implementations are wired in <c>Tessera.Host</c>.
/// </summary>
public static class DiscoveryModuleExtensions
{
    /// <summary>
    ///     Register Discovery-module services with the DI container.
    /// </summary>
    public static IServiceCollection AddDiscoveryModule(this IServiceCollection services)
    {
        services.AddSingleton<ServiceRedService>();
        return services;
    }
}
