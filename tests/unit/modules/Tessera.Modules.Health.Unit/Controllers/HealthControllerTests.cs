using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Tessera.Modules.Health.Contracts;
using Tessera.Modules.Health.Controllers;
using Tessera.Modules.Health.Errors;
using Tessera.Modules.Health.Mapping;
using Tessera.Shared.Kernel.Exceptions;
using Tessera.Shared.Kernel.Providers.Health;
using Xunit;

namespace Tessera.Modules.Health.Tests.Controllers;

/// <summary>
///     <see cref="HealthController" /> orchestration tests — verify the
///     controller delegates to <see cref="IHealthProvider" />, branches
///     correctly on the mapped <see cref="HealthStatus" />, and emits the
///     expected <see cref="ProviderException" /> shape (which the host's
///     <c>IExceptionHandler</c> translates into a 502 ProblemDetails body).
/// </summary>
public sealed class HealthControllerTests
{
    private static readonly CancellationToken Token = CancellationToken.None;

    /// <summary>
    ///     When the mapped status is <see cref="HealthStatus.Healthy" /> the
    ///     controller returns 200 OK with the response body — the caller can
    ///     inspect <c>status</c> in JSON and skip the global
    ///     <c>ProblemDetails</c> branch.
    /// </summary>
    [Fact]
    public async Task GetAsync_HealthyProvider_ReturnsOkWithBody()
    {
        var provider = Substitute.For<IHealthProvider>();
        var mapper = Substitute.For<IHealthMapper>();
        provider.CheckAsync(Token).Returns(new ProviderHealthReport("victoria-traces", HealthStatus.Healthy));
        mapper.ToResponse(Arg.Any<ProviderHealthReport>()).Returns(new HealthResponse
        {
            Provider = "victoria-traces",
            Status = HealthStatus.Healthy,
        });
        var controller = new HealthController(provider, mapper);

        var actionResult = await controller.GetAsync(Token);

        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.IsType<HealthResponse>(okResult.Value);
        await provider.Received(1).CheckAsync(Token);
    }

    /// <summary>
    ///     When the upstream reports <see cref="HealthStatus.Degraded" />
    ///     the controller throws <see cref="ProviderException" /> with
    ///     <see cref="HealthErrors.ProviderUnreachable" />. The host's
    ///     <c>IExceptionHandler</c> catches it and writes a 502
    ///     ProblemDetails body.
    /// </summary>
    [Fact]
    public async Task GetAsync_DegradedProvider_ThrowsProviderException()
    {
        var provider = Substitute.For<IHealthProvider>();
        var mapper = Substitute.For<IHealthMapper>();
        provider.CheckAsync(Token).Returns(new ProviderHealthReport("victoria-logs", HealthStatus.Degraded, "slow"));
        mapper.ToResponse(Arg.Any<ProviderHealthReport>()).Returns(new HealthResponse
        {
            Provider = "victoria-logs",
            Status = HealthStatus.Degraded,
            Detail = "slow",
        });
        var controller = new HealthController(provider, mapper);

        var exception = await Assert.ThrowsAsync<ProviderException>(
            () => controller.GetAsync(Token));

        Assert.Equal(HealthErrors.ProviderUnreachable, exception.Code);
        Assert.Contains("Degraded", exception.Message);
        Assert.Contains("slow", exception.Message);
    }

    /// <summary>
    ///     When the upstream reports <see cref="HealthStatus.Unhealthy" />
    ///     the controller also throws — same code path as Degraded. The
    ///     exception message must include the provider detail so operators can
    ///     diagnose the failure from a 502 body alone.
    /// </summary>
    [Fact]
    public async Task GetAsync_UnhealthyProvider_ThrowsProviderExceptionWithDetail()
    {
        var provider = Substitute.For<IHealthProvider>();
        var mapper = Substitute.For<IHealthMapper>();
        provider.CheckAsync(Token).Returns(new ProviderHealthReport("victoria-metrics", HealthStatus.Unhealthy, "connection refused"));
        mapper.ToResponse(Arg.Any<ProviderHealthReport>()).Returns(new HealthResponse
        {
            Provider = "victoria-metrics",
            Status = HealthStatus.Unhealthy,
            Detail = "connection refused",
        });
        var controller = new HealthController(provider, mapper);

        var exception = await Assert.ThrowsAsync<ProviderException>(
            () => controller.GetAsync(Token));

        Assert.Equal(HealthErrors.ProviderUnreachable, exception.Code);
        Assert.Contains("Unhealthy", exception.Message);
        Assert.Contains("connection refused", exception.Message);
    }

    /// <summary>
    ///     When the upstream reports degraded but carries no
    ///     <c>Detail</c> string the controller still surfaces a meaningful
    ///     exception message (the catch-all <c>"no detail"</c> branch in
    ///     the controller). Guards against a NRE on
    ///     <c>response.Detail ?? "no detail"</c>.
    /// </summary>
    [Fact]
    public async Task GetAsync_DegradedWithoutDetail_ThrowsWithFallbackMessage()
    {
        var provider = Substitute.For<IHealthProvider>();
        var mapper = Substitute.For<IHealthMapper>();
        provider.CheckAsync(Token).Returns(new ProviderHealthReport("vt", HealthStatus.Degraded, Detail: null));
        mapper.ToResponse(Arg.Any<ProviderHealthReport>()).Returns(new HealthResponse
        {
            Provider = "vt",
            Status = HealthStatus.Degraded,
            Detail = null,
        });
        var controller = new HealthController(provider, mapper);

        var exception = await Assert.ThrowsAsync<ProviderException>(
            () => controller.GetAsync(Token));

        Assert.Contains("no detail", exception.Message);
    }

    /// <summary>
    ///     The cancellation token forwarded from the controller flows through
    ///     to <see cref="IHealthProvider.CheckAsync" /> unmodified. The
    ///     provider implementation chooses whether to honour it; the
    ///     controller itself doesn't swallow or replace it.
    /// </summary>
    [Fact]
    public async Task GetAsync_ForwardsCancellationTokenToProvider()
    {
        using var cts = new CancellationTokenSource();
        var provider = Substitute.For<IHealthProvider>();
        var mapper = Substitute.For<IHealthMapper>();
        provider.CheckAsync(cts.Token).Returns(new ProviderHealthReport("vt", HealthStatus.Healthy));
        mapper.ToResponse(Arg.Any<ProviderHealthReport>()).Returns(new HealthResponse
        {
            Provider = "vt",
            Status = HealthStatus.Healthy,
        });
        var controller = new HealthController(provider, mapper);

        await controller.GetAsync(cts.Token);

        await provider.Received(1).CheckAsync(cts.Token);
    }
}
