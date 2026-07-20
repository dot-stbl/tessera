using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Tessera.LoadGen.Models;

namespace Tessera.LoadGen.Scenarios;

/// <summary>
///     <see cref="ScenarioKind.Simple" /> implementation: emits a single
///     root <see cref="Activity" />, no child spans. The cheapest
///     scenario; useful for baseline throughput and baseline latency.
/// </summary>
internal static class SimpleScenario
{
    public static async Task<bool> RunAsync(ScenarioIteration iteration, CancellationToken cancellationToken)
    {
        using var activity = iteration.ActivitySource.StartActivity(
            $"{iteration.Operation}",
            ActivityKind.Server);
        activity?.SetTag("app.scenario", "simple");
        activity?.SetTag("service.name", iteration.Service.Name);

        iteration.Logger.LogInformation(
            "Handling {Operation} for {ServiceName}",
            iteration.Operation,
            iteration.Service.Name);

        var delay = iteration.Random.Next(5, 50);
        await Task.Delay(delay, cancellationToken);

        activity?.SetTag("http.status_code", 200);
        return true;
    }
}
