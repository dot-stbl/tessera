using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Tessera.Modules.Traces.Contracts.Errors;
using Tessera.Modules.Traces.Services;
using Tessera.Shared.Kernel.Api;

namespace Tessera.Modules.Traces.Controllers;

/// <summary>
///     Errors inbox: group recent root-Error traces by exception type +
///     normalized message. Lives in the Traces module (ADR-0003).
/// </summary>
[ApiController]
[Route(ApiRoutes.Errors)]
[Tags(["errors"])]
public sealed class ErrorsController(ErrorsInboxService errorsInboxService) : ControllerBase
{
    /// <summary>
    ///     List grouped error summaries for a time window. Requires
    ///     <c>startUnixMs</c> and <c>endUnixMs</c>. Detail fetch is capped at
    ///     <see cref="ErrorsInboxService.MaxDetailFetches" />.
    /// </summary>
    [HttpGet]
    [EndpointSummary("Errors inbox: group exception-bearing error traces")]
    [ProducesResponseType<IReadOnlyList<ErrorGroupSummary>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ErrorGroupSummary>>> ListAsync(
        [FromQuery] ListErrorsRequest request,
        CancellationToken cancellationToken = default)
    {
        return Ok(await errorsInboxService.ListAsync(request, cancellationToken));
    }
}
