using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Tessera.Shared.Kernel.Api;
using Tessera.Shared.Kernel.Domain.Services;
using Tessera.Shared.Kernel.Providers.Discovery;

namespace Tessera.Modules.Discovery.Controllers;

/// <summary>
///     Service inventory endpoint. Returns all services known to the backend
///     (aggregated across traces + logs), each with its operations nested in
///     <see cref="ServiceSummary.Operations" />.
/// </summary>
[ApiController]
[Route(ApiRoutes.Services)]
public sealed class DiscoveryController(IDiscoveryProvider provider) : ControllerBase
{
    /// <summary>
    ///     List services aggregated from traces + logs.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ServiceSummary>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ServiceSummary>>> ListAsync(CancellationToken cancellationToken = default)
    {
        return Ok(await provider.ListServicesAsync(cancellationToken));
    }
}