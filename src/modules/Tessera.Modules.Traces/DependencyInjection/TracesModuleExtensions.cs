using Microsoft.Extensions.DependencyInjection;
using Tessera.Modules.Traces.Mapping;
using Tessera.Modules.Traces.Services;

namespace Tessera.Modules.Traces.DependencyInjection;

/// <summary>
///     Composition-root helper for the Traces module. Registers the
///     module-scoped Mapperly projection and request-view orchestration;
///     provider implementations are wired in <c>Tessera.Host</c>
///     (<c>project-deps-and-tests.md</c>).
/// </summary>
public static class TracesModuleExtensions
{
    /// <summary>
    ///     Register Traces-module services with the DI container.
    /// </summary>
    public static IServiceCollection AddTracesModule(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ITracesMapper, TracesMapper>();
        services.AddSingleton<RequestViewService>();
        services.AddSingleton<ErrorsInboxService>();
        return services;
    }
}
