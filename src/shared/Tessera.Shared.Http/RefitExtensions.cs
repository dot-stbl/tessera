namespace Tessera.Shared.Http;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Refit;

/// <summary>
///     Extension methods for registering Refit HTTP clients with Tessera-standard
///     resilience pipeline + bearer auth + OTel HTTP tracing.
/// </summary>
public static class RefitExtensions
{
    /// <summary>
    ///     Register a Refit client <typeparamref name="TClient" /> with default
    ///     <see cref="Configuration.HttpClientAuthOptions" />-based bearer auth,
    ///     standard Polly resilience (retries, timeouts, circuit breaker via
    ///     <c>AddStandardResilienceHandler</c>), and OTel HTTP instrumentation
    ///     (configured separately in <c>Tessera.Shared.Telemetry</c>).
    /// </summary>
    /// <param name="services">The DI service collection.</param>
    /// <param name="baseUrl">Base URL for the backend (e.g. <c>"http://localhost:10428"</c>).</param>
    /// <returns>
    ///     Resilience pipeline builder for further pipeline customization
    ///     (<c>ConfigureTimeout</c>, <c>ConfigureRetry</c>, etc.) if needed.
    /// </returns>
    public static IHttpStandardResiliencePipelineBuilder AddTesseraRefitClient<TClient>(
        this IServiceCollection services,
        string baseUrl)
        where TClient : class
    {
        return services
            .AddRefitClient<TClient>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(baseUrl))
            .AddHttpMessageHandler<BearerTokenHandler>()
            .AddStandardResilienceHandler();
    }
}