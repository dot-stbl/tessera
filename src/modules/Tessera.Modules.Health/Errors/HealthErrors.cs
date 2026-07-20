namespace Tessera.Modules.Health.Errors;

/// <summary>
///     Stable machine-readable error codes for the Health module. Used as
///     <see cref="Tessera.Shared.Kernel.Exceptions.ProviderException.Code" />
///     values when a probe fails; the global
///     <c>TesseraExceptionHandler</c> translates them into ProblemDetails
///     bodies with <c>type = "/errors/{code}"</c>.
/// </summary>
public static class HealthErrors
{
    /// <summary>
    ///     Raised by the host-side health pipeline when
    ///     <see cref="Tessera.Shared.Kernel.Providers.Health.IHealthProvider" />
    ///     reports <see cref="Tessera.Shared.Kernel.Providers.Health.HealthStatus.Degraded" />
    ///     or <see cref="Tessera.Shared.Kernel.Providers.Health.HealthStatus.Unhealthy" />.
    /// </summary>
    public const string ProviderUnreachable = "health.provider.unreachable";
}
