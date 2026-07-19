using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Refit;
using Tessera.Providers.Victoria.Clients;
using Tessera.Providers.Victoria.Configuration;
using Tessera.Providers.Victoria.Implementation;
using Tessera.Providers.Victoria.Implementation.Health;
using Tessera.Shared.Kernel.Providers.Discovery;
using Tessera.Shared.Kernel.Providers.Health;
using Tessera.Shared.Kernel.Providers.Logs;
using Tessera.Shared.Kernel.Providers.Traces;

namespace Tessera.Providers.Victoria.DependencyInjection;

/// <summary>
///     Top-level helpers for Victoria provider DI registration.
///     Split into its own file so <c>VictoriaServiceCollectionExtensions</c>
///     stays a thin one-method façade per the no-private-methods rule.
/// </summary>
public static class VictoriaServicesRegistration
{
    /// <summary>
    ///     Register the four concrete provider classes and bind each one to
    ///     its kernel provider interface via factory delegates (so callers can
    ///     resolve either the concrete type or the interface and get the same
    ///     singleton instance).
    /// </summary>
    public static void RegisterProviders(IServiceCollection services)
    {
        services.AddSingleton<VictoriaTraceProvider>();
        services.AddSingleton<VictoriaLogProvider>();
        services.AddSingleton<VictoriaDiscoveryProvider>();
        services.AddSingleton<VictoriaHealthProvider>();

        services.AddSingleton<ITraceProvider>(sp => sp.GetRequiredService<VictoriaTraceProvider>());
        services.AddSingleton<ILogProvider>(sp => sp.GetRequiredService<VictoriaLogProvider>());
        services.AddSingleton<IDiscoveryProvider>(sp => sp.GetRequiredService<VictoriaDiscoveryProvider>());
        services.AddSingleton<IHealthProvider>(sp => sp.GetRequiredService<VictoriaHealthProvider>());
    }

    /// <summary>
    ///     Build the two Refit clients (one per Victoria backend) and register
    ///     them as singletons. Each client uses a bare-metal
    ///     <see cref="HttpClient" /> for MVP-01 — the Polly resilience +
    ///     bearer auth pipeline will be wired in MVP-02 when
    ///     <c>Tessera.Shared.Http</c> helpers land.
    /// </summary>
    public static void RegisterClients(IServiceCollection services)
    {
        services.AddSingleton<IVictoriaTracesClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<VictoriaOptions>>().Value;
            var url = options.TracesUrl?.ToString() ?? "http://vt:10428";
            return RestService.For<IVictoriaTracesClient>(HttpClientFactory.CreateFor(url));
        });

        services.AddSingleton<IVictoriaLogsClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<VictoriaOptions>>().Value;
            var url = options.LogsUrl?.ToString() ?? "http://vl:9428";
            return RestService.For<IVictoriaLogsClient>(HttpClientFactory.CreateFor(url));
        });
    }
}