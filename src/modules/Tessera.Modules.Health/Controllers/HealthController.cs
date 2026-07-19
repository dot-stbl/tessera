using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Tessera.Modules.Health.Contracts;
using Tessera.Modules.Health.Mapping;
using Tessera.Shared.Kernel.Api;
using Tessera.Shared.Kernel.Providers.Health;

namespace Tessera.Modules.Health.Controllers;

/// <summary>
///     Composite health endpoint — delegates to <see cref="IHealthProvider" />
///     (wired in <c>Tessera.Host</c> composition root). Returns 200 when the
///     composite status is <see cref="HealthStatus.Healthy" />, 503 otherwise.
/// </summary>
[ApiController]
[Route(ApiRoutes.Health)]
public sealed class HealthController(IHealthProvider provider, IHealthMapper mapper) : ControllerBase
{
    /// <summary>
    ///     Probe all wired providers and return the composite status.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<HealthResponse>> GetAsync(CancellationToken cancellationToken = default)
    {
        var report = await provider.CheckAsync(cancellationToken);
        var response = mapper.ToResponse(report);
        return response.Status == HealthStatus.Healthy
            ? Ok(response)
            : StatusCode(StatusCodes.Status503ServiceUnavailable, response);
    }
}
