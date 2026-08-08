using System.Net;
using Tessera.Integration.Fixtures;
using Tessera.Integration.Support;
using Xunit;

namespace Tessera.Integration.Scenarios;

/// <summary>Wave-1: Tessera.Host health against the seeded Victoria stack.</summary>
[Collection(Wave1CollectionNames.Name)]
public sealed class HostHealthScenario(Wave1Fixture fixture)
{
    /// <summary>
    ///     When TESSERA_IT=1, GET /api/v1/health returns 200 after seed+host start.
    /// </summary>
    [Fact]
    public async Task Host_Health_Returns200WhenStackSeeded()
    {
        if (!IntegrationGate.IsEnabled || fixture.HostClient is null)
        {
            return;
        }

        using var response = await fixture.HostClient.GetAsync("api/v1/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
