using System.Net;
using System.Net.Http.Json;
using Tessera.LoadGen.Models;
using Tessera.Stack.Integration.Collections;
using Tessera.Stack.Integration.Configuration;
using Tessera.Stack.Testing.Fixtures;
using Tessera.Stack.Testing.Helpers;

namespace Tessera.Stack.Integration.Discovery;

/// <summary>
///     Pushes a synthetic trace to the OTel collector, waits for
///     the collector to flush to <c>VictoriaTraces</c>, then asserts
///     that <c>GET /api/v1/services</c> round-trips the synthetic
///     service names back to the caller through
///     <c>Tessera.Providers.Victoria</c>'s
///     <c>IDiscoveryProvider.GetServicesAsync</c>.
/// </summary>
/// <remarks>
///     The synthetic services match <see cref="DefaultServiceFactory" />
///     to keep the assertions stable regardless of whether the
///     <c>Tessera.LoadGen</c> console produced them or this test
///     fixture did.
/// </remarks>
[Collection(StackCollection.Name)]
public sealed class DiscoveryRoundTripShould(TesseraStackFixture fixture)
{
    private static readonly string[] ExpectedServices =
    [
        "checkout-svc",
        "payment-svc",
    ];

    private readonly TesseraStackFixture _fixture = fixture;

    [IntegrationFact]
    public async Task SyntheticTracesSurfaceInServicesDiscovery()
    {
        var services = DefaultServiceFactory.Materialise(ExpectedServices);
        var pushed = await OtlpPushHelper.PushTracesAsync(
            otlpEndpoint: new Uri("http://localhost:4317"),
            services: services,
            duration: TimeSpan.FromSeconds(3),
            cancellationToken: default).ConfigureAwait(false);
        Assert.True(pushed > 0, "Load generator should have pushed at least one trace.");

        // VictoriaTraces indexes new spans over a ~1-second ingest
        // window; sleep the OTel collector's batch interval before
        // querying.
        await Task.Delay(TimeSpan.FromSeconds(2)).ConfigureAwait(false);

        using var client = _fixture.CreateClient();
        var response = await client.GetAsync("/api/v1/services").ConfigureAwait(false);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<string>>().ConfigureAwait(false);
        Assert.NotNull(body);
        foreach (var expected in ExpectedServices)
        {
            Assert.Contains(expected, body);
        }
    }

    /// <summary>
    /// Response payload shape — minimal projection. The real response
    /// is a richer record; this local DTO exists only to receive the
    /// JSON array of service names without dragging the provider
    /// package into the test project.
    /// </summary>
    /// <param name="Services"></param>
    private sealed record ServicesResponse(IReadOnlyList<string> Services);
}
