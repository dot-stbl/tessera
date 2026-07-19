using Tessera.Modules.Health.Contracts;
using Tessera.Modules.Health.Mapping;
using Tessera.Shared.Kernel.Providers.Health;
using Xunit;

namespace Tessera.Modules.Health.Tests.Mapping;

/// <summary>
///     <see cref="HealthMapper" /> projection tests — verify Mapperly
///     source-gen mapping of domain <see cref="ProviderHealthReport" />
///     (string + enum + nullable detail) to wire DTO
///     <see cref="HealthResponse" />. Mapper is stateless; register as
///     singleton in DI.
/// </summary>
public sealed class HealthMapperTests
{
    /// <summary>
    ///     Healthy status maps with the provider name preserved verbatim —
    ///     the wire DTO carries no additional info beyond what the domain
    ///     record held.
    /// </summary>
    [Fact]
    public void ToResponse_Healthy_PreservesProviderAndStatus()
    {
        var mapper = new HealthMapper();

        var response = mapper.ToResponse(new ProviderHealthReport("victoria-traces", HealthStatus.Healthy));

        Assert.Equal("victoria-traces", response.Provider);
        Assert.Equal(HealthStatus.Healthy, response.Status);
        Assert.Null(response.Detail);
    }

    /// <summary>
    ///     Degraded status with a non-empty detail maps through unchanged —
    ///     the DTO exposes Detail for the FE status panels.
    /// </summary>
    [Fact]
    public void ToResponse_DegradedWithDetail_PreservesDetail()
    {
        var mapper = new HealthMapper();

        var response = mapper.ToResponse(new ProviderHealthReport("victoria-logs", HealthStatus.Degraded, "sustained 503s"));

        Assert.Equal("victoria-logs", response.Provider);
        Assert.Equal(HealthStatus.Degraded, response.Status);
        Assert.Equal("sustained 503s", response.Detail);
    }

    /// <summary>
    ///     The Mapperly partial method actually exists in the generated
    ///     source — if the source generator ever fails to emit it (project
    ///     refactor, removal of <c>[Mapper]</c>, etc.) this test fails at
    ///     compile time via <c>partial</c> resolution.
    /// </summary>
    [Fact]
    public void Mapperly_GeneratedPartial_ResolvesAtRuntime()
    {
        var mapper = new HealthMapper();

        Assert.IsAssignableFrom<IHealthMapper>(mapper);
        Assert.NotNull(mapper.ToResponse(new ProviderHealthReport("vt", HealthStatus.Healthy)));
    }
}
