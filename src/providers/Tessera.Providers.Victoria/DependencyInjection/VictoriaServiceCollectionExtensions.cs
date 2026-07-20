using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tessera.Providers.Victoria.Configuration;

namespace Tessera.Providers.Victoria.DependencyInjection;

/// <summary>
///     Façade extension for wiring the Victoria provider into DI. The
///     host composition root calls <c>AddVictoriaProvider(configuration)</c>
///     once; per-module DI chains are isolated from this implementation.
/// </summary>
public static class VictoriaServiceCollectionExtensions
{
    /// <summary>
    ///     Bind <see cref="VictoriaOptions" /> from the <c>victoria</c>
    ///     section of <paramref name="configuration" />, validate on start.
    ///     <c>ErrorOnUnknownConfiguration = false</c> keeps backward
    ///     compatibility with sidecar docs that include extra keys during
    ///     Phase 6 transition; tighten once config schema is locked.
    /// </summary>
    public static IServiceCollection AddVictoriaProvider(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<VictoriaOptions>()
            .Bind(configuration.GetSection("victoria"), static options => options.ErrorOnUnknownConfiguration = false)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.ResolveVictoriaSecrets();

        VictoriaServicesRegistration.RegisterProviders(services);
        VictoriaServicesRegistration.RegisterClients(services);
        return services;
    }
}
