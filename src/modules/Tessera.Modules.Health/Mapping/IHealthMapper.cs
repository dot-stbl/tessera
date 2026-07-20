using Tessera.Modules.Health.Contracts;
using Tessera.Shared.Kernel.Providers.Health;

namespace Tessera.Modules.Health.Mapping;

/// <summary>
///     Domain <c>ProviderHealthReport</c> → wire DTO <c>HealthResponse</c>.
///     Hand-fronted interface so the endpoint depends on the contract, not on
///     the generated partial implementation.
/// </summary>
public interface IHealthMapper
{
    /// <summary>
    ///     Project a <see cref="ProviderHealthReport" /> into the JSON-friendly
    ///     <see cref="HealthResponse" /> shape returned by <c>GET /api/health</c>.
    /// </summary>
    public HealthResponse ToResponse(ProviderHealthReport report);
}
