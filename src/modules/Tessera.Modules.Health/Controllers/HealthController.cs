using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Tessera.Modules.Health.Contracts;
using Tessera.Modules.Health.Errors;
using Tessera.Modules.Health.Mapping;
using Tessera.Shared.Kernel.Api;
using Tessera.Shared.Kernel.Exceptions;
using Tessera.Shared.Kernel.Providers.Health;

namespace Tessera.Modules.Health.Controllers;

/// <summary>
///     Composite health endpoint — delegates to <see cref="IHealthProvider" />
///     (wired in <c>Tessera.Host</c> composition root). Returns 200 when the
///     aggregate status is <see cref="HealthStatus.Healthy" />, and a
///     ProblemDetails non-2xx (502) otherwise. Non-2xx responses are
///     documented globally by the host's <c>ProblemDetailsResponsesTransformer</c>.
/// </summary>
[ApiController]
[Route(ApiRoutes.Health)]
[Tags(["health"])]
public sealed class HealthController(IHealthProvider provider, IHealthMapper mapper) : ControllerBase
{
    /// <summary>
    ///     Probe all wired providers and return the composite status. Returns
    ///     200 with the report body when the aggregate is <see cref="HealthStatus.Healthy" />;
    ///     on degraded / unhealthy it throws <see cref="ProviderException" />
    ///     with <see cref="HealthErrors.ProviderUnreachable" /> and the host's
    ///     <c>IExceptionHandler</c> writes a 502 ProblemDetails body — the
    ///     controller body shape is single-valued by design so FE consumers
    ///     inspect <c>status</c> unconditionally on error.
    /// </summary>
    [HttpGet]
    [EndpointSummary("Composite health status across all wired providers")]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<HealthResponse>> GetAsync(CancellationToken cancellationToken = default)
    {
        var report = await provider.CheckAsync(cancellationToken);
        var response = mapper.ToResponse(report);

        if (response.Status == HealthStatus.Healthy)
        {
            return Ok(response);
        }

        throw new ProviderException(
            HealthErrors.ProviderUnreachable,
            $"health probe reported {response.Status}: {response.Detail ?? "no detail"}");
    }
}
