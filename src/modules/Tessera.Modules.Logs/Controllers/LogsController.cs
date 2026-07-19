using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Tessera.Modules.Logs.Contracts;
using Tessera.Shared.Kernel.Api;
using Tessera.Shared.Kernel.Domain.Logs;
using Tessera.Shared.Kernel.Pagination;
using Tessera.Shared.Kernel.Providers.Logs;

namespace Tessera.Modules.Logs.Controllers;

/// <summary>
///     Logs endpoints. MVP-01 restricts to the trace-correlation path; ad-hoc
///     LogsQL is deferred to MVP-02.
/// </summary>
[ApiController]
[Route(ApiRoutes.Logs)]
public sealed class LogsController(ILogProvider provider) : ControllerBase
{
    /// <summary>
    ///     Single-element array re-used on every 400 response so the validation
    ///     error dictionary doesn't allocate per request (CA1861).
    /// </summary>
    private static readonly string[] TraceIdRequiredMessage =
        ["traceId is required for MVP-01 log queries."];

    /// <summary>
    ///     List logs by trace id. Returns 400 when <c>traceId</c> is missing.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<Page<LogEntry>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Page<LogEntry>>> ListAsync(
        [FromQuery] ListLogsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.ToLogQuery() is not { } query)
        {
            ModelState.AddModelError("traceId", TraceIdRequiredMessage[0]);
            return ValidationProblem(ModelState);
        }

        var page = await provider.QueryAsync(query, cancellationToken);
        return Ok(page);
    }
}