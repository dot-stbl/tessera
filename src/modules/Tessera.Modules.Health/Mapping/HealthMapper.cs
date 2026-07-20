using Riok.Mapperly.Abstractions;
using Tessera.Modules.Health.Contracts;
using Tessera.Shared.Kernel.Providers.Health;

namespace Tessera.Modules.Health.Mapping;

/// <summary>
///     Source-generated Mapperly projection of
///     <see cref="ProviderHealthReport" /> → <see cref="HealthResponse" />.
///     Stateless; registered as a singleton in DI.
/// </summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public sealed partial class HealthMapper : IHealthMapper
{
    /// <inheritdoc />
    public partial HealthResponse ToResponse(ProviderHealthReport report);
}
