using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Tessera.Modules.Traces.Contracts;
using Tessera.Modules.Traces.Errors;
using Tessera.Modules.Traces.Mapping;
using Tessera.Modules.Traces.Services;
using Tessera.Shared.Kernel.Api;
using Tessera.Shared.Kernel.Domain.Traces;
using Tessera.Shared.Kernel.Exceptions;
using Tessera.Shared.Kernel.Identifiers;
using Tessera.Shared.Kernel.Pagination;
using Tessera.Shared.Kernel.Providers.Traces;

namespace Tessera.Modules.Traces.Controllers;

/// <summary>
///     Traces endpoints. Two routes:
///     <list type="bullet">
///         <item><c>GET /api/v1/traces</c> — cursor-paginated trace search.</item>
///         <item>
///             <c>GET /api/v1/traces/{traceId:length(32)}</c> — degradable
///             request view (spans + correlated logs).
///         </item>
///     </list>
///     404 only when both spans and logs are empty
///     (<see cref="ProviderNotFoundException" /> /
///     <see cref="TracesErrors.TraceNotFound" />).
/// </summary>
[ApiController]
[Route(ApiRoutes.Traces)]
[Tags(["traces"])]
public sealed class TracesController(
    ITraceProvider traceProvider,
    RequestViewService requestViewService,
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
    ///     Degradable request view for <paramref name="traceId" />. Returns
    ///     200 when either spans or logs exist; throws
    ///     <see cref="ProviderNotFoundException" /> only when both are empty.
    ///     Route uses <c>:length(32)</c> (Tessera trace ids are 32-char hex
    ///     per OpenTelemetry/Jaeger); malformed ids 404 at the router before
    ///     the handler runs.
    /// </summary>
    /// <exception cref="ProviderNotFoundException"></exception>
    [HttpGet(ApiRoutes.TraceByIdRelative)]
    [EndpointSummary("Request view: trace + correlated logs (degradable)")]
    [ProducesResponseType<GetTraceResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<GetTraceResponse>> GetAsync(
        [FromRoute] string traceId,
        CancellationToken cancellationToken = default)
    {
        var view = await requestViewService.GetAsync(new TraceId(traceId), cancellationToken);
        return Ok(mapper.ToResponse(view));
    }
}
