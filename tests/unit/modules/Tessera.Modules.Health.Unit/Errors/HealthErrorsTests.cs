using Tessera.Modules.Health.Errors;
using Xunit;

namespace Tessera.Modules.Health.Tests.Errors;

/// <summary>
///     <see cref="HealthErrors" /> static contract tests — verify the
///     machine-readable <c>Code</c> constants are stable dot.case strings
///     (consumed by FE clients branching on <c>problem.code</c>) and never
///     change shape. Per <c>error-mapping.md</c> §2: clients branch on
///     <c>Code</c>, never on <c>Message</c>.
/// </summary>
public sealed class HealthErrorsTests
{
    /// <summary>
    ///     The only error code in MVP-01 is <c>health.provider.unreachable</c>
    ///     — emitted when the composite health probe reports Degraded or
    ///     Unhealthy and the controller throws <see cref="Tessera.Shared.Kernel.Exceptions.ProviderException" />.
    ///     Locking the literal prevents accidental renames that would silently
    ///     break every consumer that branches on this code.
    /// </summary>
    [Fact]
    public void ProviderUnreachable_IsStableDotCase()
    {
        Assert.Equal("health.provider.unreachable", HealthErrors.ProviderUnreachable);
    }
}
