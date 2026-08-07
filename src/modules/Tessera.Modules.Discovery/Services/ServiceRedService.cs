using Tessera.Modules.Discovery.Contracts;
using Tessera.Modules.Discovery.Services.Red;
using Tessera.Shared.Kernel.Analysis.Red;
using Tessera.Shared.Kernel.Exceptions;
using Tessera.Shared.Kernel.Providers.Discovery;
using Tessera.Shared.Kernel.Providers.Metrics;

namespace Tessera.Modules.Discovery.Services;

/// <summary>
///     Orchestrates service RED: PromQL metrics first, SpanApprox fallback on
///     any <see cref="ProviderException" /> from the metrics backend
///     (including unconfigured).
/// </summary>
public sealed class ServiceRedService(
    IMetricsProvider metricsProvider,
    IDiscoveryProvider discoveryProvider)
{
    /// <summary>
    ///     Compute RED for <paramref name="serviceName" /> over the request window.
    ///     Instant queries evaluate at <see cref="GetServiceRedRequest.EndUnixMs" />.
    /// </summary>
    /// <exception cref="ProviderException">Invalid range or empty service name.</exception>
    /// <exception cref="ProviderNotFoundException">Service missing on SpanApprox path.</exception>
    public async Task<RedSnapshot> GetAsync(
        string serviceName,
        GetServiceRedRequest request,
        CancellationToken cancellationToken = default)
    {
        ServiceRedValidation.EnsureServiceName(serviceName);
        ServiceRedValidation.EnsureValidRange(request);

        try
        {
            return await ServiceRedAssembly.QueryMetricsAsync(
                metricsProvider,
                serviceName,
                request.Operation,
                request.EndUnixMs,
                cancellationToken);
        }
        catch (ProviderException)
        {
            return await ServiceRedAssembly.FromDiscoveryAsync(
                discoveryProvider,
                serviceName,
                cancellationToken);
        }
    }
}
