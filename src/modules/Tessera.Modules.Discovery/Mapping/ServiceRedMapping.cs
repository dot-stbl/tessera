using Tessera.Modules.Discovery.Contracts;
using Tessera.Shared.Kernel.Analysis.Red;

namespace Tessera.Modules.Discovery.Mapping;

/// <summary>
///     Domain <see cref="RedSnapshot" /> → HTTP <see cref="ServiceRedResponse" />.
/// </summary>
internal static class ServiceRedMapping
{
    /// <summary>Map a domain snapshot to the wire DTO.</summary>
    public static ServiceRedResponse ToResponse(RedSnapshot snapshot)
    {
        return new ServiceRedResponse
        {
            RequestRatePerSec = snapshot.RequestRatePerSec,
            ErrorRatio = snapshot.ErrorRatio,
            DurationP95Ms = snapshot.DurationP95Ms,
            Source = snapshot.Source,
        };
    }
}
