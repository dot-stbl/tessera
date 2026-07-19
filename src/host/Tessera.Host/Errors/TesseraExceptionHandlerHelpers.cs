using Microsoft.AspNetCore.Http;
using Tessera.Shared.Kernel.Exceptions;

namespace Tessera.Host.Errors;

/// <summary>
///     Status-code mapping for the global <see cref="TesseraExceptionHandler" />.
///     <see cref="ProviderNotFoundException" /> → 404,
///     <see cref="ProviderTimeoutException" /> → 504, base
///     <see cref="ProviderException" /> → 502. Lives in a
///     <c>file static class</c> so the <see cref="TesseraExceptionHandler" />
///     stays focused on response shaping and does not carry private methods
///     (code-shape.md §9 ban).
/// </summary>
internal static class TesseraExceptionHandlerHelpers
{
    public static int MapStatus(ProviderException exception)
    {
        return exception switch
        {
            ProviderNotFoundException => StatusCodes.Status404NotFound,
            ProviderTimeoutException => StatusCodes.Status504GatewayTimeout,
            _ => StatusCodes.Status502BadGateway,
        };
    }
}
