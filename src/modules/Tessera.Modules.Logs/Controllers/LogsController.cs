using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Tessera.Modules.Logs.Contracts;
using Tessera.Modules.Logs.Errors;
using Tessera.Shared.Kernel.Api;
using Tessera.Shared.Kernel.Domain.Logs;
using Tessera.Shared.Kernel.Exceptions;
using Tessera.Shared.Kernel.Pagination;
using Tessera.Shared.Kernel.Providers.Logs;

namespace Tessera.Modules.Logs.Controllers;

/// <summary>
///     Logs endpoints. MVP-01 restricts to the trace-correlation path; ad-hoc
///     LogsQL is deferred to MVP-02.
/// </summary>
[ApiController]
[Route(ApiRoutes.Logs)]
[Tags(["logs"])]
public sealed class LogsController(ILogProvider provider) : ControllerBase
{
    /// <summary>
    ///     List logs by trace id. Throws
    ///     <see cref="ProviderException" /> with
    ///     <see cref="LogsErrors.TraceIdRequired" /> when <c>traceId</c> is
    ///     missing — the global <c>IExceptionHandler</c> maps that to a 400
    ///     ProblemDetails body. MVC's built-in model-binding 400 (malformed
    ///     <c>traceId</c>, etc.) is unrelated and stays on the default
    ///     ValidationProblem path. Non-2xx responses are documented globally
    ///     by the host's <c>ProblemDetailsResponsesTransformer</c>.
    /// </summary>
    [HttpGet]
    [EndpointSummary("List logs by trace id (MVP-01). Ad-hoc LogsQL — MVP-02.")]
    [ProducesResponseType<Page<LogEntry>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<Page<LogEntry>>> ListAsync(
        [FromQuery] ListLogsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.ToLogQuery() is not { } query)
        {
            throw new ProviderException(
                LogsErrors.TraceIdRequired,
                "traceId is required for MVP-01 log queries.");
        }

        var page = await provider.QueryAsync(query, cancellationToken);
        return Ok(page);
    }
}