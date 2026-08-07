using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Tessera.Modules.Discovery.Contracts;
using Tessera.Modules.Discovery.Controllers;
using Tessera.Modules.Discovery.Services;
using Tessera.Shared.Kernel.Analysis.Red;
using Tessera.Shared.Kernel.Domain.Metrics.Results;
using Tessera.Shared.Kernel.Domain.Metrics.Samples;
using Tessera.Shared.Kernel.Domain.Metrics.Series;
using Tessera.Shared.Kernel.Exceptions;
using Tessera.Shared.Kernel.Providers.Discovery;
using Tessera.Shared.Kernel.Providers.Metrics;

namespace Tessera.Modules.Discovery.Unit.Controllers;

/// <summary>
///     <see cref="ServiceRedController" /> smoke — maps snapshot to response DTO.
/// </summary>
public sealed class ServiceRedControllerTests
{
    private static readonly IReadOnlyDictionary<string, string> EmptyLabels =
        new Dictionary<string, string>();

    private static MetricVector Vector(double value)
    {
        return new MetricVector(
        [
            new MetricSeries(EmptyLabels, [new MetricSample(2_000L, value)]),
        ]);
    }

    /// <inheritdoc/>
    [Fact]
    public async Task GetAsync_ReturnsMappedOkBody()
    {
        var metrics = Substitute.For<IMetricsProvider>();
        var discovery = Substitute.For<IDiscoveryProvider>();
        metrics.QueryInstantAsync(Arg.Any<MetricsInstantQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                callInfo =>
                {
                    var query = callInfo.Arg<MetricsInstantQuery>().Query;
                    if (query.Contains("histogram_quantile", StringComparison.Ordinal))
                    {
                        return Vector(0.1);
                    }

                    if (query.Contains("5..", StringComparison.Ordinal))
                    {
                        return Vector(0.4);
                    }

                    return Vector(4.0);
                });
        var controller = new ServiceRedController(new ServiceRedService(metrics, discovery));
        var request = new GetServiceRedRequest
        {
            StartUnixMs = 1_000L,
            EndUnixMs = 2_000L,
        };

        var actionResult = await controller.GetAsync("api", request);

        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var body = Assert.IsType<ServiceRedResponse>(okResult.Value);
        Assert.Equal(RedSource.Metrics, body.Source);
        Assert.Equal(4.0, body.RequestRatePerSec);
        Assert.Equal(0.1, body.ErrorRatio);
        Assert.Equal(100.0, body.DurationP95Ms);
    }

    /// <inheritdoc/>
    [Fact]
    public async Task GetAsync_InvalidRange_PropagatesProviderException()
    {
        var metrics = Substitute.For<IMetricsProvider>();
        var discovery = Substitute.For<IDiscoveryProvider>();
        var controller = new ServiceRedController(new ServiceRedService(metrics, discovery));
        var request = new GetServiceRedRequest
        {
            StartUnixMs = 0,
            EndUnixMs = 0,
        };

        await Assert.ThrowsAsync<ProviderException>(
            () => controller.GetAsync("api", request));
    }
}
