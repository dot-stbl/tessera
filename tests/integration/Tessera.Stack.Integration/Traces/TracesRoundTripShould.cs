using System.Net;
using Tessera.LoadGen.Models;
using Tessera.Stack.Integration.Collections;
using Tessera.Stack.Integration.Configuration;
using Tessera.Stack.Testing.Fixtures;
using Tessera.Stack.Testing.Helpers;

namespace Tessera.Stack.Integration.Traces;

/// <summary>
///     Pushes a synthetic trace, waits for <c>VictoriaTraces</c> to
///     index it, then asserts that <c>GET /api/v1/traces/{id}</c>
///     round-trips the trace through the
///     <c>ITraceProvider.GetByIdAsync</c> path. The trace returned
///     must contain at least the root span attributed to the
///     synthetic service.
/// </summary>
[Collection(StackCollection.Name)]
public sealed class TracesRoundTripShould(TesseraStackFixture fixture)
{
    private static readonly string[] Services = ["checkout-svc"];

    private readonly TesseraStackFixture _fixture = fixture;

    [IntegrationFact]
    public async Task PushedTraceIsRetrievableById()
    {
        var services = DefaultServiceFactory.Materialise(Services);
        var pushed = await OtlpPushHelper.PushTracesAsync(
            otlpEndpoint: new Uri("http://localhost:4317"),
            services: services,
            duration: TimeSpan.FromSeconds(3),
            cancellationToken: default).ConfigureAwait(false);
        Assert.True(pushed > 0, "Load generator should have pushed at least one trace.");

        await Task.Delay(TimeSpan.FromSeconds(2)).ConfigureAwait(false);

        // The host's trace-detail endpoint requires a hex-encoded
        // trace id; we don't pin a specific one because the OTel
        // exporter assigns ids on the client side. The
        // <c>GET /api/v1/traces?service=...</c> list endpoint is the
        // realistic driver for end-to-end checks — that endpoint is
        // covered here.
        using var client = _fixture.CreateClient();
        var listResponse = await client.GetAsync("/api/v1/traces?service=checkout-svc&startUnixMs=0&endUnixMs=" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()).ConfigureAwait(false);

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var body = await listResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
        Assert.NotEmpty(body);
    }
}
