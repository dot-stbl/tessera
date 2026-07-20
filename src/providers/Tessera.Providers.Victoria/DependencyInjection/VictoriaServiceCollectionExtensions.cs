using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Tessera.Providers.Victoria.Configuration;

namespace Tessera.Providers.Victoria.DependencyInjection;

/// <summary>
///     DI extension for the Victoria stack provider (MVP-01).
///     Façade over <see cref="VictoriaServicesRegistration" /> — Tessera.Host
///     calls <c>services.AddVictoriaProvider(...)</c> from its composition root.
/// </summary>
public static class VictoriaServiceCollectionExtensions
{
    /// <summary>
    ///     Bind <see cref="VictoriaOptions" /> from the <c>victoria</c>
    ///     configuration section, register the provider implementations and
    ///     their kernel interface mappings, and register the Refit HTTP
    ///     clients for both backends.
    /// </summary>
    public static IServiceCollection AddVictoriaProvider(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<VictoriaOptions>()
            .Bind(configuration.GetSection("victoria"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        VictoriaServicesRegistration.RegisterProviders(services);
        VictoriaServicesRegistration.RegisterClients(services);

        return services;
    }
}
