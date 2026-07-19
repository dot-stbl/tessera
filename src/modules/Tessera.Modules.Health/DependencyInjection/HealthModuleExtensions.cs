using Microsoft.Extensions.DependencyInjection;
using Tessera.Modules.Health.Mapping;

namespace Tessera.Modules.Health.DependencyInjection;

/// <summary>
///     Composition-root helper for the Health module. Registers the
///     module-scoped Mapperly projection under <see cref="IHealthMapper" />;
///     the <see cref="Tessera.Shared.Kernel.Providers.Health.IHealthProvider" />
///     implementation is wired in <c>Tessera.Host</c>, not here
///     (<c>project-deps-and-tests.md</c>).
/// </summary>
public static class HealthModuleExtensions
{
    /// <summary>
    ///     Register Health-module services with the DI container.
    /// </summary>
    public static IServiceCollection AddHealthModule(this IServiceCollection services)
    {
        services.AddSingleton<IHealthMapper, HealthMapper>();
        return services;
    }
}