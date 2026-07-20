using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Tessera.Providers.Victoria.Clients;
using Tessera.Providers.Victoria.Configuration;
using Tessera.Providers.Victoria.Implementation;
using Tessera.Providers.Victoria.Implementation.Health;
using Tessera.Shared.Http;
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

        services.AddSingleton<ITraceProvider>(static sp => sp.GetRequiredService<VictoriaTraceProvider>());
        services.AddSingleton<ILogProvider>(static sp => sp.GetRequiredService<VictoriaLogProvider>());
        services.AddSingleton<IDiscoveryProvider>(static sp => sp.GetRequiredService<VictoriaDiscoveryProvider>());
        services.AddSingleton<IHealthProvider>(static sp => sp.GetRequiredService<VictoriaHealthProvider>());
    }

    /// <summary>
    ///     Build the two Refit clients (one per Victoria backend) and register
    ///     them as singletons. Each client uses
    ///     <see cref="RefitExtensions.AddTesseraRefitClient{TClient}" /> so the
    ///     bearer-auth + standard Polly resilience pipeline is shared with the
    ///     rest of Tessera (per http-resilience-refit.md §2 — bare
    ///     <c>new HttpClient()</c> is banned).
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    public static void RegisterClients(IServiceCollection services)
    {
        services.AddTesseraRefitClient<IVictoriaTracesClient>(
            services.BuildServiceProvider().GetRequiredService<IOptions<VictoriaOptions>>().Value.TracesUrl?.ToString()
                ?? throw new InvalidOperationException("VictoriaOptions.TracesUrl is required at startup"));
        services.AddTesseraRefitClient<IVictoriaLogsClient>(
            services.BuildServiceProvider().GetRequiredService<IOptions<VictoriaOptions>>().Value.LogsUrl?.ToString()
                ?? throw new InvalidOperationException("VictoriaOptions.LogsUrl is required at startup"));
    }
}
