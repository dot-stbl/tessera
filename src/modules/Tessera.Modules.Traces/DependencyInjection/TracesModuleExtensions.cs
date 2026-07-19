using Microsoft.Extensions.DependencyInjection;
using Tessera.Modules.Traces.Mapping;

namespace Tessera.Modules.Traces.DependencyInjection;

/// <summary>
///     Composition-root helper for the Traces module. Registers the
///     module-scoped Mapperly projection under <see cref="ITracesMapper" />;
///     the <see cref="Tessera.Shared.Kernel.Providers.Traces.ITraceProvider" />
///     and <see cref="Tessera.Shared.Kernel.Providers.Logs.ILogProvider" />
///     implementations are wired in <c>Tessera.Host</c>, not here
///     (<c>project-deps-and-tests.md</c>).
/// </summary>
public static class TracesModuleExtensions
{
    /// <summary>
    ///     Register Traces-module services with the DI container.
    /// </summary>
    public static IServiceCollection AddTracesModule(this IServiceCollection services)
    {
        services.AddSingleton<ITracesMapper, TracesMapper>();
        return services;
    }
}