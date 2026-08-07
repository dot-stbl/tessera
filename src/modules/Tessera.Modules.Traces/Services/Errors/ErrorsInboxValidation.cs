using Tessera.Modules.Traces.Contracts.Errors;
using Tessera.Modules.Traces.Errors;
using Tessera.Shared.Kernel.Exceptions;

namespace Tessera.Modules.Traces.Services.Errors;

/// <summary>
///     Request validation for the errors inbox (stable error codes).
/// </summary>
internal static class ErrorsInboxValidation
{
    /// <summary>
    ///     Require a non-zero, ordered time range. Throws
    ///     <see cref="ProviderException" /> with
    ///     <see cref="TracesErrors.SearchInvalid" /> (maps to 400).
    /// </summary>
    /// <exception cref="ProviderException"></exception>
    public static void EnsureValidRange(ListErrorsRequest request)
    {
        if (request.StartUnixMs <= 0 || request.EndUnixMs <= 0)
        {
            throw new ProviderException(
                TracesErrors.SearchInvalid,
                "startUnixMs and endUnixMs are required and must be positive");
        }

        if (request.EndUnixMs < request.StartUnixMs)
        {
            throw new ProviderException(
                TracesErrors.SearchInvalid,
                "endUnixMs must not precede startUnixMs");
        }
    }
}
