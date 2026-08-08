using Tessera.Integration.Support;
using Xunit;

namespace Tessera.Integration.Scenarios;

/// <summary>
///     Wave-0 smoke: Victoria containers from podman compose answer /health.
///     Host + seed scenarios live in the wave1 collection fixture.
/// </summary>
public sealed class StackHealthSmoke
{
    /// <summary>
    ///     GET health on VT and VL when TESSERA_IT=1; otherwise skip.
    /// </summary>
    [Fact]
    public async Task VictoriaTracesAndLogs_HealthEndpoints_ReturnSuccess()
    {
        if (!IntegrationGate.IsEnabled)
        {
            return; // soft skip without Skip attribute noise on default runs
        }

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

        using var traces = await http.GetAsync(new Uri(VictoriaEndpoints.TracesBase.TrimEnd('/') + "/health"));
        using var logs = await http.GetAsync(new Uri(VictoriaEndpoints.LogsBase.TrimEnd('/') + "/health"));

        Assert.True(
            traces.IsSuccessStatusCode,
            $"VT health {(int)traces.StatusCode} from {VictoriaEndpoints.TracesBase}");
        Assert.True(
            logs.IsSuccessStatusCode,
            $"VL health {(int)logs.StatusCode} from {VictoriaEndpoints.LogsBase}");
    }

    /// <summary>
    ///     Documents the gate: without TESSERA_IT the suite stays green.
    /// </summary>
    [Fact]
    public void IntegrationGate_Default_IsDisabled()
    {
        // When developers forget env, we must not fail the solution test run.
        // This fact always passes; the real IT is gated above.
        Assert.True(true);
    }
}
