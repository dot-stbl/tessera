using Tessera.Modules.Discovery.Contracts;
using Tessera.Modules.Discovery.Errors;
using Tessera.Shared.Kernel.Exceptions;

namespace Tessera.Modules.Discovery.Services.Red;

/// <summary>
///     Request validation for service RED queries.
/// </summary>
internal static class ServiceRedValidation
{
    /// <summary>
    ///     Require a non-zero, ordered time range.
    /// </summary>
    /// <exception cref="ProviderException"></exception>
    public static void EnsureValidRange(GetServiceRedRequest request)
    {
        if (request.StartUnixMs <= 0 || request.EndUnixMs <= 0)
        {
            throw new ProviderException(
                DiscoveryErrors.RedInvalid,
                "startUnixMs and endUnixMs are required and must be positive");
        }

        if (request.EndUnixMs < request.StartUnixMs)
        {
            throw new ProviderException(
                DiscoveryErrors.RedInvalid,
                "endUnixMs must not precede startUnixMs");
        }
    }

    /// <summary>
    ///     Require a non-empty service name path segment.
    /// </summary>
    /// <exception cref="ProviderException"></exception>
    public static void EnsureServiceName(string serviceName)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            throw new ProviderException(
                DiscoveryErrors.RedInvalid,
                "serviceName is required");
        }
    }
}
