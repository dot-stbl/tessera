using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Tessera.Shared.Kernel.Api;
using Tessera.Shared.Kernel.Exceptions;

namespace Tessera.Shared.Web.Errors;

/// <summary>
///     Global <see cref="IExceptionHandler" /> that converts a
///     <see cref="ProviderException" /> (or its
///     <see cref="ProviderNotFoundException" /> /
///     <see cref="ProviderTimeoutException" /> specializations) into a
///     <see cref="ProblemDetails" /> body. One handler for the whole app —
///     per-endpoint try/catch is banned (see <c>api-design.md</c> §5).
/// </summary>
public sealed class TesseraExceptionHandler(ILogger<TesseraExceptionHandler> logger) : IExceptionHandler
{
    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not ProviderException providerException)
        {
            return false;
        }

        var statusCode = TesseraExceptionHandlerHelpers.MapStatus(providerException);

        var problem = new ProblemDetails
        {
            Type = $"/errors/{providerException.Code}",
            Title = providerException.Code,
            Status = statusCode,
            Detail = exception.Message,
            Instance = httpContext.Request.Path,
            Extensions =
            {
                ["code"] = providerException.Code,
            },
        };

#pragma warning disable CA1848
        logger.LogWarning(
            exception,
            "Provider failure on {Path}: {Code} -> {StatusCode}",
            httpContext.Request.Path,
            providerException.Code,
            statusCode);
#pragma warning restore CA1848

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(
            problem,
            TesseraJsonOptions.Instance,
            cancellationToken);
        return true;
    }
}
