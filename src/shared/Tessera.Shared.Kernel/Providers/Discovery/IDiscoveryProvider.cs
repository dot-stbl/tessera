namespace Tessera.Shared.Kernel.Providers.Discovery;

using Tessera.Shared.Kernel.Domain.Services;

/// <summary>
/// Abstraction over a service-discovery backend. Returns the inventory of
/// services observed in traces/logs.
/// </summary>
public interface IDiscoveryProvider
{
    /// <summary>
    /// List all known services with aggregated metrics (span counts, error counts, operations).
    /// </summary>
    Task<IReadOnlyList<ServiceSummary>> ListServicesAsync(CancellationToken ct);
}