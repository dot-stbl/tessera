using Tessera.Modules.Discovery.Errors;
using Tessera.Shared.Kernel.Analysis.Red;
using Tessera.Shared.Kernel.Domain.Services;
using Tessera.Shared.Kernel.Exceptions;
using Tessera.Shared.Kernel.Providers.Discovery;
using Tessera.Shared.Kernel.Providers.Metrics;

namespace Tessera.Modules.Discovery.Services.Red;

/// <summary>
///     Metrics instant queries + SpanApprox inventory fallback for service RED.
/// </summary>
internal static class ServiceRedAssembly
{
    /// <summary>
    ///     Query request rate, 5xx rate, and p95 duration; assemble a Metrics
    ///     <see cref="RedSnapshot" />. Error ratio = errorRate / requestRate.
    /// </summary>
    public static async Task<RedSnapshot> QueryMetricsAsync(
        IMetricsProvider metrics,
        string serviceName,
        string? operation,
        long timeUnixMs,
        CancellationToken cancellationToken)
    {
        var rateQuery = RedPromQl.RequestRate(serviceName, operation);
        var errorQuery = RedPromQl.ErrorRate(serviceName, operation);
        var durationQuery = RedPromQl.DurationP95(serviceName, operation);

        var rateVector = await metrics.QueryInstantAsync(
            new MetricsInstantQuery(rateQuery, timeUnixMs),
            cancellationToken);
        var errorVector = await metrics.QueryInstantAsync(
            new MetricsInstantQuery(errorQuery, timeUnixMs),
            cancellationToken);
        var durationVector = await metrics.QueryInstantAsync(
            new MetricsInstantQuery(durationQuery, timeUnixMs),
            cancellationToken);

        var requestRate = RedMetricExtract.FirstSampleValue(rateVector);
        var errorRate = RedMetricExtract.FirstSampleValue(errorVector);
        var durationSeconds = RedMetricExtract.FirstSampleValue(durationVector);

        return new RedSnapshot(
            RequestRatePerSec: requestRate,
            ErrorRatio: RedMetricExtract.SafeRatio(errorRate, requestRate),
            DurationP95Ms: RedMetricExtract.SecondsToMilliseconds(durationSeconds),
            Source: RedSource.Metrics);
    }

    /// <summary>
    ///     List services and map the matching name to a SpanApprox snapshot.
    /// </summary>
    /// <exception cref="ProviderNotFoundException">When the service is absent.</exception>
    public static async Task<RedSnapshot> FromDiscoveryAsync(
        IDiscoveryProvider discovery,
        string serviceName,
        CancellationToken cancellationToken)
    {
        var services = await discovery.ListServicesAsync(cancellationToken);
        if (FindByName(services, serviceName) is not { } summary)
        {
            throw new ProviderNotFoundException(
                DiscoveryErrors.ServiceNotFound,
                $"service '{serviceName}' not found");
        }

        return FromSummary(summary);
    }

    /// <summary>
    ///     Build a SpanApprox snapshot from a service inventory row.
    ///     ErrorRatio = ErrorCount / SpanCount when SpanCount &gt; 0.
    ///     RequestRatePerSec and DurationP95Ms stay null (no window density / no p95).
    /// </summary>
    public static RedSnapshot FromSummary(ServiceSummary summary)
    {
        double? errorRatio = summary.SpanCount > 0
            ? (double)summary.ErrorCount / summary.SpanCount
            : null;

        return new RedSnapshot(
            RequestRatePerSec: null,
            ErrorRatio: errorRatio,
            DurationP95Ms: null,
            Source: RedSource.SpanApprox);
    }

    /// <summary>
    ///     Case-sensitive name match against inventory.
    /// </summary>
    public static ServiceSummary? FindByName(
        IReadOnlyList<ServiceSummary> services,
        string serviceName)
    {
        foreach (var service in services)
        {
            if (string.Equals(service.Name, serviceName, StringComparison.Ordinal))
            {
                return service;
            }
        }

        return null;
    }
}
