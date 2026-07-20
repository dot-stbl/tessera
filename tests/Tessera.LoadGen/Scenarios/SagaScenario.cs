using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Tessera.LoadGen.Models;

namespace Tessera.LoadGen.Scenarios;

/// <summary>
///     <see cref="ScenarioKind.Saga" /> implementation: root
///     <see cref="Activity" /> with a sequence of forward activities
///     followed by compensating activities on failure. Models a
///     long-running transaction-style flow.
/// </summary>
internal static class SagaScenario
{
    public static async Task<bool> RunAsync(ScenarioIteration iteration, CancellationToken cancellationToken)
    {
        using var root = iteration.ActivitySource.StartActivity(
            $"{iteration.Operation}",
            ActivityKind.Server);
        root?.SetTag("app.scenario", "saga");
        root?.SetTag("service.name", iteration.Service.Name);

        iteration.Logger.LogInformation(
            "Saga: {ServiceName} → {Operation}",
            iteration.Service.Name,
            iteration.Operation);

        var steps = new[] { "reserve", "charge", "ship" };
        var compensate = false;
        foreach (var step in steps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using (var forward = iteration.ActivitySource.StartActivity(
                $"forward {step}",
                ActivityKind.Internal,
                parentContext: root?.Context ?? default))
            {
                forward?.SetTag("saga.step", step);
                await Task.Delay(iteration.Random.Next(5, 25), cancellationToken);
                forward?.SetTag("saga.result", "ok");
            }

            // ~30 % chance the third step fails → compensate.
            if (step == "charge" && iteration.Random.NextDouble() < 0.3)
            {
                compensate = true;
                break;
            }
        }

        if (compensate)
        {
            foreach (var step in steps.Reverse())
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var backward = iteration.ActivitySource.StartActivity(
                    $"compensate {step}",
                    ActivityKind.Internal,
                    parentContext: root?.Context ?? default);
                backward?.SetTag("saga.step", step);
                backward?.SetTag("saga.compensating", true);
                iteration.Logger.LogWarning(
                    "Saga compensating step {Step} for {ServiceName}",
                    step,
                    iteration.Service.Name);
                await Task.Delay(iteration.Random.Next(2, 15), cancellationToken);
            }
            root?.SetTag("saga.outcome", "compensated");
            root?.SetTag("http.status_code", 409);
            return false;
        }

        root?.SetTag("saga.outcome", "committed");
        root?.SetTag("http.status_code", 201);
        return true;
    }
}
