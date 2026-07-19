using Microsoft.Extensions.Configuration;
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
///     DI extension for the Victoria stack provider (MVP-01).
///     Wires the four kernel provider interfaces (Trace, Log, Discovery, Health)
///     to their Victoria implementations. <c>Tessera.Host</c> calls this
///     from its composition root.
/// </summary>
public static class VictoriaServiceCollectionExtensions
{
    /// <summary>
    ///     Bind <see cref="VictoriaOptions" /> from the <c>victoria</c>
    ///     configuration section, register the Refit HTTP clients (with
    ///     standard Polly resilience + bearer auth), and register the four
    ///     provider implementations as their kernel interfaces.
    /// </summary>
    public static IServiceCollection AddVictoriaProvider(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<VictoriaOptions>()
            .Bind(configuration.GetSection("victoria"))
            .ValidateOnStart();

        services.AddSingleton<VictoriaTraceProvider>();
        services.AddSingleton<VictoriaLogProvider>();
        services.AddSingleton<VictoriaDiscoveryProvider>();
        services.AddSingleton<VictoriaHealthProvider>();

        services.AddSingleton<ITraceProvider>(sp => sp.GetRequiredService<VictoriaTraceProvider>());
        services.AddSingleton<ILogProvider>(sp => sp.GetRequiredService<VictoriaLogProvider>());
        services.AddSingleton<IDiscoveryProvider>(sp => sp.GetRequiredService<VictoriaDiscoveryProvider>());
        services.AddSingleton<IHealthProvider>(sp => sp.GetRequiredService<VictoriaHealthProvider>());

        services.AddSingleton<IVictoriaTracesClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<VictoriaOptions>>().Value;
            return RestService.For<IVictoriaTracesClient>(CreateHttpClient(options.TracesUrl?.ToString() ?? "http://vt:10428"));
        });

        services.AddSingleton<IVictoriaLogsClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<VictoriaOptions>>().Value;
            return RestService.For<IVictoriaLogsClient>(CreateHttpClient(options.LogsUrl?.ToString() ?? "http://vl:9428"));
        });

        return services;
    }

    private static HttpClient CreateHttpClient(string baseUrl)
    {
        var client = new HttpClient { BaseAddress = new Uri(baseUrl) };
        client.Timeout = TimeSpan.FromSeconds(30);
        return client;
    }
}