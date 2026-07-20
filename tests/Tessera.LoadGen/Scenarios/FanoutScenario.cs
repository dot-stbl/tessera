using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Tessera.LoadGen.Models;

namespace Tessera.LoadGen.Scenarios;

/// <summary>
///     <see cref="ScenarioKind.Fanout" /> implementation: root
///     <see cref="Activity" /> with 2–3 child spans representing a
///     service making simultaneous calls to its dependencies (DB,
///     cache, downstream API). Models the most common
///     microservice span shape.
/// </summary>
internal static class FanoutScenario
{
    public static async Task<bool> RunAsync(ScenarioIteration iteration, CancellationToken cancellationToken)
    {
        using var root = iteration.ActivitySource.StartActivity(
            $"{iteration.Operation}",
            ActivityKind.Server);
        root?.SetTag("app.scenario", "fanout");
        root?.SetTag("service.name", iteration.Service.Name);

        iteration.Logger.LogInformation(
            "Fanout: {ServiceName} → {Operation}",
            iteration.Service.Name,
            iteration.Operation);

        var childCount = iteration.Random.Next(2, 4);
        var tasks = new List<Task<bool>>(childCount);
        for (var index = 0; index < childCount; index++)
        {
            var dependency = $"dep-{iteration.Random.Next(1, 6)}";
            tasks.Add(EmitChildAsync(iteration, root, dependency, cancellationToken));
        }

        var results = await Task.WhenAll(tasks);
        var ok = results.All(static value => value);
        root?.SetTag("http.status_code", ok ? 200 : 502);
        return ok;
    }

    private static async Task<bool> EmitChildAsync(
        ScenarioIteration iteration,
        Activity? root,
        string dependency,
        CancellationToken cancellationToken)
    {
        using var child = iteration.ActivitySource.StartActivity(
            $"call {dependency}",
            ActivityKind.Client,
            parentContext: root?.Context ?? default);
        child?.SetTag("app.scenario", "fanout-child");
        child?.SetTag("dependency.name", dependency);

        iteration.Logger.LogDebug(
            "Fanout child {Dependency} for {ServiceName}",
            dependency,
            iteration.Service.Name);

        var delay = iteration.Random.Next(2, 20);
        await Task.Delay(delay, cancellationToken);
        child?.SetTag("http.status_code", 200);
        return true;
    }
}
