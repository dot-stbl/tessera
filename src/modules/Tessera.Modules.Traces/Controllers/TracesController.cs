using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Tessera.Modules.Traces.Contracts;
using Tessera.Modules.Traces.Endpoints;
using Tessera.Modules.Traces.Mapping;
using Tessera.Shared.Kernel.Api;
using Tessera.Shared.Kernel.Domain.Traces;
using Tessera.Shared.Kernel.Identifiers;
using Tessera.Shared.Kernel.Pagination;
using Tessera.Shared.Kernel.Providers.Logs;
using Tessera.Shared.Kernel.Providers.Traces;

namespace Tessera.Modules.Traces.Controllers;

/// <summary>
///     Traces endpoints. Two routes:
///     <list type="bullet">
///         <item><c>GET /api/traces</c> — cursor-paginated trace search.</item>
///         <item>
///             <c>GET /api/traces/{traceId}</c> — full trace + correlated logs
///             in a single response (Kibana Observability style).
///         </item>
///     </list>
/// </summary>
[ApiController]
[Route(ApiRoutes.Traces)]
public sealed class TracesController(
    ITraceProvider traceProvider,
    ILogProvider logProvider,
    ITracesMapper mapper) : ControllerBase
{
    /// <summary>
    ///     Search traces by service / operation / time range / duration.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<Page<TraceSummary>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<Page<TraceSummary>>> ListAsync(
        [FromQuery] ListTracesRequest request,
        CancellationToken cancellationToken = default)
    {
        var page = await traceProvider.SearchAsync(request.ToTraceSearchQuery(), cancellationToken);
        return Ok(page);
    }

    /// <summary>
    ///     Trace detail with correlated logs in a single response. Returns 404
    ///     when the trace id is not found upstream.
    /// </summary>
    [HttpGet("{traceId}")]
    [ProducesResponseType<GetTraceResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GetTraceResponse>> GetAsync(
        [FromRoute] string traceId,
        CancellationToken cancellationToken = default)
    {
        var parsedTraceId = new TraceId(traceId);
        var trace = await traceProvider.GetByIdAsync(parsedTraceId, cancellationToken);
        if (trace is null)
        {
            return NotFound();
        }

        var range = TracesEndpointHelpers.ToLogCorrelationRange(trace);
        var logs = await logProvider.ListByTraceAsync(parsedTraceId, range, cancellationToken);
        return Ok(mapper.ToResponse(trace, logs));
    }
}