using Microsoft.Extensions.DependencyInjection;

namespace Tessera.Modules.Logs.DependencyInjection;

/// <summary>
///     Composition-root helper for the Logs module. The module has no
///     module-scoped services (it consumes <see cref="Tessera.Shared.Kernel.Providers.Logs.ILogProvider" />
///     directly), so this extension is intentionally empty — it's here so
///     <c>Program.cs</c> has a consistent <c>AddXxxModule()</c> chain across
///     all four feature modules.
/// </summary>
public static class LogsModuleExtensions
{
    /// <summary>
    ///     Register Logs-module services with the DI container.
    /// </summary>
    public static IServiceCollection AddLogsModule(this IServiceCollection services)
    {
        return services;
    }
}