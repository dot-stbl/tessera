using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Tessera.Modules.Traces.Contracts;
using Tessera.Modules.Traces.Endpoints;
using Tessera.Modules.Traces.Errors;
using Tessera.Modules.Traces.Mapping;
using Tessera.Shared.Kernel.Api;
using Tessera.Shared.Kernel.Domain.Traces;
using Tessera.Shared.Kernel.Exceptions;
using Tessera.Shared.Kernel.Identifiers;
using Tessera.Shared.Kernel.Pagination;
using Tessera.Shared.Kernel.Providers.Logs;
using Tessera.Shared.Kernel.Providers.Traces;

namespace Tessera.Modules.Traces.Controllers;

/// <summary>
///     Traces endpoints. Two routes:
///     <list type="bullet">
///         <item><c>GET /api/v1/traces</c> — cursor-paginated trace search.</item>
///         <item>
///             <c>GET /api/v1/traces/{traceId:length(32)}</c> — full trace +
///             correlated logs in a single response (Kibana Observability style).
///         </item>
///     </list>
///     404 surface path: <see cref="ProviderNotFoundException" /> with
///     <see cref="TracesErrors.TraceNotFound" /> code (non-2xx responses
///     are documented globally by the host's
///     <c>ProblemDetailsResponsesTransformer</c>).
/// </summary>
[ApiController]
[Route(ApiRoutes.Traces)]
[Tags(["traces"])]
public sealed class TracesController(
    ITraceProvider traceProvider,
    ILogProvider logProvider,
    ITracesMapper mapper) : ControllerBase
{
    /// <summary>
    ///     Search traces by service / operation / time range / duration.
    ///     Returns 200 with a cursor-paginated page; validation failures
    ///     (model binding) become 400 ProblemDetails via the global filter.
    /// </summary>
    [HttpGet]
    [EndpointSummary("Search traces by service / operation / time / duration")]
    [ProducesResponseType<Page<TraceSummary>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<Page<TraceSummary>>> ListAsync(
        [FromQuery] ListTracesRequest request,
        CancellationToken cancellationToken = default)
    {
        var page = await traceProvider.SearchAsync(request.ToTraceSearchQuery(), cancellationToken);
        return Ok(page);
    }

    /// <summary>
    ///     Trace detail with correlated logs in a single response. Throws
    ///     <see cref="ProviderNotFoundException" /> with
    ///     <see cref="TracesErrors.TraceNotFound" /> when the trace id is not
    ///     found upstream — the global <c>IExceptionHandler</c> translates
    ///     that into a 404 ProblemDetails body.
    ///     Route uses <c>:length(32)</c> (Tessera trace ids are 32-char hex
    ///     per OpenTelemetry/Jaeger); malformed ids 404 at the router before
    ///     the handler runs.
    /// </summary>
    [HttpGet(ApiRoutes.Trace)]
    [EndpointSummary("Trace detail with correlated logs (single response)")]
    [ProducesResponseType<GetTraceResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<GetTraceResponse>> GetAsync(
        [FromRoute] string traceId,
        CancellationToken cancellationToken = default)
    {
        var parsedTraceId = new TraceId(traceId);
        var trace = await traceProvider.GetByIdAsync(parsedTraceId, cancellationToken)
            ?? throw new ProviderNotFoundException(
                TracesErrors.TraceNotFound,
                $"trace {traceId} not found");

        var range = TracesEndpointHelpers.ToLogCorrelationRange(trace);
        var logs = await logProvider.ListByTraceAsync(parsedTraceId, range, cancellationToken);
        return Ok(mapper.ToResponse(trace, logs));
    }
}