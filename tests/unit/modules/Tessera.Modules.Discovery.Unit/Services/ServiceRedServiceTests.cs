using NSubstitute;
using Tessera.Modules.Discovery.Contracts;
using Tessera.Modules.Discovery.Errors;
using Tessera.Modules.Discovery.Services;
using Tessera.Shared.Kernel.Analysis.Red;
using Tessera.Shared.Kernel.Domain.Metrics.Results;
using Tessera.Shared.Kernel.Domain.Metrics.Samples;
using Tessera.Shared.Kernel.Domain.Metrics.Series;
using Tessera.Shared.Kernel.Domain.Services;
using Tessera.Shared.Kernel.Exceptions;
using Tessera.Shared.Kernel.Providers.Discovery;
using Tessera.Shared.Kernel.Providers.Metrics;

namespace Tessera.Modules.Discovery.Unit.Services;

/// <summary>
///     <see cref="ServiceRedService" /> — metrics success, metrics failure → span
///     approx, missing service on fallback.
/// </summary>
public sealed class ServiceRedServiceTests
{
    private static readonly IReadOnlyDictionary<string, string> EmptyLabels =
        new Dictionary<string, string>();

    private static GetServiceRedRequest ValidRequest(string? operation = null)
    {
        return new GetServiceRedRequest
        {
            StartUnixMs = 1_000L,
            EndUnixMs = 2_000L,
            Operation = operation,
        };
    }

    private static MetricVector Vector(double value)
    {
        return new MetricVector(
        [
            new MetricSeries(EmptyLabels, [new MetricSample(2_000L, value)]),
        ]);
    }

    private static void StubMetrics(
        IMetricsProvider metrics,
        double rate,
        double errorRate,
        double durationSeconds)
    {
        metrics.QueryInstantAsync(Arg.Any<MetricsInstantQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                callInfo =>
                {
                    var query = callInfo.Arg<MetricsInstantQuery>().Query;
                    if (query.Contains("histogram_quantile", StringComparison.Ordinal))
                    {
                        return Vector(durationSeconds);
                    }

                    if (query.Contains("5..", StringComparison.Ordinal))
                    {
                        return Vector(errorRate);
                    }

                    return Vector(rate);
                });
    }

    /// <inheritdoc/>
    [Fact]
    public async Task GetAsync_MetricsSuccess_ReturnsMetricsSource()
    {
        var metrics = Substitute.For<IMetricsProvider>();
        var discovery = Substitute.For<IDiscoveryProvider>();
        StubMetrics(metrics, rate: 10.0, errorRate: 1.0, durationSeconds: 0.25);
        var service = new ServiceRedService(metrics, discovery);

        var snapshot = await service.GetAsync("checkout", ValidRequest());

        Assert.Equal(RedSource.Metrics, snapshot.Source);
        Assert.Equal(10.0, snapshot.RequestRatePerSec);
        Assert.Equal(0.1, snapshot.ErrorRatio);
        Assert.Equal(250.0, snapshot.DurationP95Ms);
        await discovery.DidNotReceive().ListServicesAsync(Arg.Any<CancellationToken>());
    }

    /// <inheritdoc/>
    [Fact]
    public async Task GetAsync_MetricsThrows_FallsBackToSpanApprox()
    {
        var metrics = Substitute.For<IMetricsProvider>();
        var discovery = Substitute.For<IDiscoveryProvider>();
        metrics.QueryInstantAsync(Arg.Any<MetricsInstantQuery>(), Arg.Any<CancellationToken>())
            .Returns<Task<MetricVector>>(static _ => throw new ProviderException(
                "provider.metrics_not_configured",
                "metrics backend not configured"));
        discovery.ListServicesAsync(Arg.Any<CancellationToken>())
            .Returns(
            [
                new ServiceSummary("checkout", SpanCount: 100, ErrorCount: 5, Operations: []),
            ]);
        var service = new ServiceRedService(metrics, discovery);

        var snapshot = await service.GetAsync("checkout", ValidRequest());

        Assert.Equal(RedSource.SpanApprox, snapshot.Source);
        Assert.Null(snapshot.RequestRatePerSec);
        Assert.Equal(0.05, snapshot.ErrorRatio);
        Assert.Null(snapshot.DurationP95Ms);
    }

    /// <inheritdoc/>
    [Fact]
    public async Task GetAsync_MetricsThrowsAndServiceMissing_ThrowsNotFound()
    {
        var metrics = Substitute.For<IMetricsProvider>();
        var discovery = Substitute.For<IDiscoveryProvider>();
        metrics.QueryInstantAsync(Arg.Any<MetricsInstantQuery>(), Arg.Any<CancellationToken>())
            .Returns<Task<MetricVector>>(_ => throw new ProviderException(
                "provider.network_error",
                "vm down"));
        discovery.ListServicesAsync(Arg.Any<CancellationToken>())
            .Returns([new ServiceSummary("other", 1, 0, [])]);
        var service = new ServiceRedService(metrics, discovery);

        var thrown = await Assert.ThrowsAsync<ProviderNotFoundException>(
            () => service.GetAsync("checkout", ValidRequest()));

        Assert.Equal(DiscoveryErrors.ServiceNotFound, thrown.Code);
    }

    /// <inheritdoc/>
    [Fact]
    public async Task GetAsync_InvalidRange_ThrowsBeforeMetrics()
    {
        var metrics = Substitute.For<IMetricsProvider>();
        var discovery = Substitute.For<IDiscoveryProvider>();
        var service = new ServiceRedService(metrics, discovery);
        var request = new GetServiceRedRequest
        {
            StartUnixMs = 2_000L,
            EndUnixMs = 1_000L,
        };

        var thrown = await Assert.ThrowsAsync<ProviderException>(
            () => service.GetAsync("checkout", request));

        Assert.Equal(DiscoveryErrors.RedInvalid, thrown.Code);
        await metrics.DidNotReceive()
            .QueryInstantAsync(Arg.Any<MetricsInstantQuery>(), Arg.Any<CancellationToken>());
    }

    /// <inheritdoc/>
    [Fact]
    public async Task GetAsync_MetricsSuccess_ForwardsEndUnixMsAndOperation()
    {
        var metrics = Substitute.For<IMetricsProvider>();
        var discovery = Substitute.For<IDiscoveryProvider>();
        StubMetrics(metrics, 1.0, 0.0, 0.01);
        var service = new ServiceRedService(metrics, discovery);

        await service.GetAsync("checkout", ValidRequest("GET /pay"));

        await metrics.Received().QueryInstantAsync(
            Arg.Is<MetricsInstantQuery>(static query =>
                query.TimeUnixMs == 2_000L
                && query.Query.Contains("http_route=\"GET /pay\"", StringComparison.Ordinal)),
            Arg.Any<CancellationToken>());
    }
}
