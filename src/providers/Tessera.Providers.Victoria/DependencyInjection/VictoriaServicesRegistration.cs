using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Tessera.Providers.Victoria.Clients;
using Tessera.Providers.Victoria.Configuration;
using Tessera.Providers.Victoria.Implementation;
using Tessera.Providers.Victoria.Implementation.Health;
using Tessera.Providers.Victoria.Implementation.Metrics;
using Tessera.Shared.Http;
using Tessera.Shared.Kernel.Providers.Discovery;
using Tessera.Shared.Kernel.Providers.Health;
using Tessera.Shared.Kernel.Providers.Logs;
using Tessera.Shared.Kernel.Providers.Metrics;
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
    ///     Register the concrete provider classes and bind each one to
    ///     its kernel provider interface via factory delegates (so callers can
    ///     resolve either the concrete type or the interface and get the same
    ///     singleton instance). Metrics uses <see cref="VictoriaMetricsProvider" />
    ///     when <c>victoria.metrics.url</c> is set; otherwise
    ///     <see cref="UnconfiguredMetricsProvider" />.
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

        RegisterMetricsProvider(services);
    }

    /// <summary>
    ///     Build Refit clients (traces, logs, optional metrics) and register
    ///     them as singletons. Each client uses
    ///     <see cref="RefitExtensions.AddTesseraRefitClient{TClient}" /> so the
    ///     bearer-auth + standard Polly resilience pipeline is shared with the
    ///     rest of Tessera (per http-resilience-refit.md §2 — bare
    ///     <c>new HttpClient()</c> is banned).
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The BuildServiceProvider() call is scoped to this helper's
    ///         body and disposed before the method returns — a temporary
    ///         root provider for resolving <see cref="IOptions{TOptions}" />
    ///         at registration time. The di-installer.md §5 banned-pattern
    ///         warning about <c>BuildServiceProvider()</c> applies when the
    ///         root provider leaks into request scope; here it does not.
    ///     </para>
    /// </remarks>
    public static void RegisterClients(IServiceCollection services)
    {
        using var tempProvider = services.BuildServiceProvider();
        var options = tempProvider.GetRequiredService<IOptions<VictoriaOptions>>().Value;

        services.AddTesseraRefitClient<IVictoriaTracesClient>(
            options.Traces.Url.ToString());
        services.AddTesseraRefitClient<IVictoriaLogsClient>(
            options.Logs.Url.ToString());

        if (options.Metrics?.Url is { } metricsUrl)
        {
            services.AddTesseraRefitClient<IVictoriaMetricsClient>(metricsUrl.ToString());
        }
    }

    /// <summary>
    ///     Register metrics provider based on whether Metrics.Url is configured.
    ///     Must run after options binding so the temporary provider can read it.
    /// </summary>
    public static void RegisterMetricsProvider(IServiceCollection services)
    {
        using var tempProvider = services.BuildServiceProvider();
        var options = tempProvider.GetRequiredService<IOptions<VictoriaOptions>>().Value;

        if (options.Metrics?.Url is not null)
        {
            services.AddSingleton<VictoriaMetricsProvider>();
            services.AddSingleton<IMetricsProvider>(static sp =>
                sp.GetRequiredService<VictoriaMetricsProvider>());
        }
        else
        {
            services.AddSingleton<IMetricsProvider, UnconfiguredMetricsProvider>();
        }
    }
}
