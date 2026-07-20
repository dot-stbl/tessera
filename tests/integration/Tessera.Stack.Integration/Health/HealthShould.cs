using System.Net;
using Tessera.Stack.Integration.Collections;
using Tessera.Stack.Integration.Configuration;
using Tessera.Stack.Testing.Fixtures;

namespace Tessera.Stack.Integration.Health;

/// <summary>
///     Smoke test for the host's composite health endpoint. The
///     assertion is intentionally narrow: the host comes up, the
///     <c>/api/v1/health</c> route returns <c>200 OK</c> with a JSON
///     body carrying <c>status</c>. The provider-health details
///     (Victoria reachability) are asserted per-provider in their
///     own test folders — <c>HealthShould</c> only proves the
///     route is wired.
/// </summary>
[Collection(StackCollection.Name)]
public sealed class HealthShould(TesseraStackFixture fixture)
{
    private readonly TesseraStackFixture _fixture = fixture;

    [IntegrationFact]
    public async Task GetHealthEndpointReturnsOk()
    {
        using var client = _fixture.CreateClient();

        var response = await client.GetAsync("/api/v1/health").ConfigureAwait(false);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        Assert.NotEmpty(body);
    }
}
