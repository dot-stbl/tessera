using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Tessera.Modules.Discovery.Contracts;
using Tessera.Modules.Discovery.Mapping;
using Tessera.Modules.Discovery.Services;
using Tessera.Shared.Kernel.Api;

namespace Tessera.Modules.Discovery.Controllers;

/// <summary>
///     Service RED endpoint: rate / errors / duration via metrics with span
///     fallback (ADR-0002 §4, ADR-0003).
/// </summary>
[ApiController]
[Route(ApiRoutes.ServiceRed)]
[Tags(["discovery"])]
public sealed class ServiceRedController(ServiceRedService serviceRedService) : ControllerBase
{
    /// <summary>
    ///     RED snapshot for one service. Requires positive ordered
    ///     <c>startUnixMs</c> / <c>endUnixMs</c>. Optional <c>operation</c>
    ///     filters PromQL by <c>http_route</c>.
    /// </summary>
    [HttpGet]
    [EndpointSummary("Service RED (rate / errors / duration)")]
    [ProducesResponseType<ServiceRedResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ServiceRedResponse>> GetAsync(
        [FromRoute] string serviceName,
        [FromQuery] GetServiceRedRequest request,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await serviceRedService.GetAsync(serviceName, request, cancellationToken);
        return Ok(ServiceRedMapping.ToResponse(snapshot));
    }
}
