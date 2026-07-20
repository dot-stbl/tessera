using System.Net;
using Tessera.LoadGen.Models;
using Tessera.Stack.Integration.Collections;
using Tessera.Stack.Integration.Configuration;
using Tessera.Stack.Testing.Fixtures;
using Tessera.Stack.Testing.Helpers;

namespace Tessera.Stack.Integration.Logs;

/// <summary>
///     Smoke test for the <c>GET /api/v1/logs</c> endpoint. Pushes a
///     synthetic trace to seed both <c>VictoriaTraces</c> and
///     <c>VictoriaLogs</c> via the OTel collector, then asserts the
///     route's validation contract: requests without a
///     <c>traceId</c> query parameter are rejected with 400, mirroring
///     the unit-test behaviour of <c>LogsController</c>.
/// </summary>
[Collection(StackCollection.Name)]
public sealed class LogsRoundTripShould(TesseraStackFixture fixture)
{
    private static readonly string[] Services = ["checkout-svc"];

    private readonly TesseraStackFixture _fixture = fixture;

    [IntegrationFact]
    public async Task ListEndpointWithoutTraceIdReturnsValidationProblem()
    {
        var services = DefaultServiceFactory.Materialise(Services);
        await OtlpPushHelper.PushTracesAsync(
            otlpEndpoint: new Uri("http://localhost:4317"),
            services: services,
            duration: TimeSpan.FromSeconds(2),
            cancellationToken: default).ConfigureAwait(false);

        using var client = _fixture.CreateClient();
        var response = await client.GetAsync("/api/v1/logs").ConfigureAwait(false);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
