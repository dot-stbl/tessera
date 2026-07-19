using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Tessera.Modules.Discovery.Controllers;
using Tessera.Shared.Kernel.Domain.Services;
using Tessera.Shared.Kernel.Exceptions;
using Tessera.Shared.Kernel.Providers.Discovery;
using Xunit;

namespace Tessera.Modules.Discovery.Tests.Controllers;

/// <summary>
///     <see cref="DiscoveryController" /> orchestration tests — the
///     controller is a pure delegate to <see cref="IDiscoveryProvider" />
///     with no business branching, so the surface under test reduces to:
///     (a) forward + returns provider list as 200 OK body, (b) propagate
///     upstream failures as <see cref="ProviderException" /> for the host
///     <c>IExceptionHandler</c> to map to ProblemDetails, (c) forward the
///     cancellation token.
/// </summary>
public sealed class DiscoveryControllerTests
{
    /// <summary>
    ///     The controller returns the list as-is wrapped in 200 OK —
    ///     <see cref="IDiscoveryProvider" /> is the source of truth for what
    ///     services exist, the controller adds no aggregation.
    /// </summary>
    [Fact]
    public async Task ListAsync_ReturnsProviderListAsOkBody()
    {
        var provider = Substitute.For<IDiscoveryProvider>();
        var gatewayOps = new List<ServiceOperation>().AsReadOnly();
        var paymentsOps = new List<ServiceOperation>().AsReadOnly();
        var services = new List<ServiceSummary>
        {
            new("api-gateway", 142, 3, gatewayOps),
            new("payments",   98,  0, paymentsOps),
        };
        provider.ListServicesAsync(Arg.Any<CancellationToken>()).Returns(services);
        var controller = new DiscoveryController(provider);

        var actionResult = await controller.ListAsync();

        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var body = Assert.IsAssignableFrom<IReadOnlyList<ServiceSummary>>(okResult.Value);
        Assert.Equal(2, body.Count);
        Assert.Equal("api-gateway", body[0].Name);
    }

    /// <summary>
    ///     When the provider has nothing matching yet (cold start, all-Victoria
    ///     flags down), the controller surfaces an empty list — not null,
    ///     not a 5xx. FE consumes <c>body.length</c> and treats 0 as legitimate.
    /// </summary>
    [Fact]
    public async Task ListAsync_EmptyProvider_Returns200WithEmptyArray()
    {
        var provider = Substitute.For<IDiscoveryProvider>();
        provider.ListServicesAsync(Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ServiceSummary>());
        var controller = new DiscoveryController(provider);

        var actionResult = await controller.ListAsync();

        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var body = Assert.IsAssignableFrom<IReadOnlyList<ServiceSummary>>(okResult.Value);
        Assert.Empty(body);
    }

    /// <summary>
    ///     Upstream <see cref="ProviderException" /> propagates verbatim —
    ///     controller has no try/catch (per-endpoint try/catch banned by
    ///     api-design.md §5), so the global <c>IExceptionHandler</c> in
    ///     host composition root catches it and writes a ProblemDetails body.
    /// </summary>
    [Fact]
    public async Task ListAsync_UpstreamProviderException_Propagates()
    {
        var provider = Substitute.For<IDiscoveryProvider>();
        var upstream = new ProviderException("provider.network_error", "vt unreachable");
        provider.ListServicesAsync(Arg.Any<CancellationToken>())
            .Returns<System.Threading.Tasks.Task<IReadOnlyList<ServiceSummary>>>(_ => throw upstream);
        var controller = new DiscoveryController(provider);

        var thrown = await Assert.ThrowsAsync<ProviderException>(() => controller.ListAsync());

        Assert.Same(upstream, thrown);
    }

    /// <summary>
    ///     Cancellation token forwarded unmodified to the provider — the
    ///     controller doesn't replace it (e.g. with
    ///     <c>HttpContext.RequestAborted</c>) so callers passing
    ///     <c>CancellationToken.None</c> get exactly that.
    /// </summary>
    [Fact]
    public async Task ListAsync_ForwardsCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var provider = Substitute.For<IDiscoveryProvider>();
        provider.ListServicesAsync(cts.Token)
            .Returns(Array.Empty<ServiceSummary>());
        var controller = new DiscoveryController(provider);

        await controller.ListAsync(cts.Token);

        await provider.Received(1).ListServicesAsync(cts.Token);
    }
}
