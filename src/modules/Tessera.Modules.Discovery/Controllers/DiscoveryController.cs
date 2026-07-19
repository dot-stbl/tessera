using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Tessera.Shared.Kernel.Api;
using Tessera.Shared.Kernel.Domain.Services;
using Tessera.Shared.Kernel.Providers.Discovery;

namespace Tessera.Modules.Discovery.Controllers;

/// <summary>
///     Service inventory endpoint. Returns all services known to the backend
///     (aggregated across traces + logs), each with its operations nested in
///     <see cref="ServiceSummary.Operations" />. MVP-01 has no per-module
///     error codes for Discovery — upstream failures surface as
///     <see cref="Tessera.Shared.Kernel.Exceptions.ProviderException" /> with
///     <c>provider.network_error</c> / <c>provider.timeout</c> (lives in
///     <c>Tessera.Shared.Kernel.Exceptions</c>), not module-specific codes.
/// </summary>
[ApiController]
[Route(ApiRoutes.Services)]
[Tags(["discovery"])]
public sealed class DiscoveryController(IDiscoveryProvider provider) : ControllerBase
{
    /// <summary>
    ///     List services aggregated from traces + logs.
    /// </summary>
    [HttpGet]
    [EndpointSummary("Service inventory aggregated from traces + logs")]
    [ProducesResponseType<IReadOnlyList<ServiceSummary>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ServiceSummary>>> ListAsync(CancellationToken cancellationToken = default)
    {
        return Ok(await provider.ListServicesAsync(cancellationToken));
    }
}