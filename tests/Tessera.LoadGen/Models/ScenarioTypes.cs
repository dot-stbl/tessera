using Microsoft.Extensions.Logging;
using Tessera.LoadGen.Models;
using Tessera.LoadGen.Scenarios;

namespace Tessera.LoadGen.Models;

/// <summary>
///     Renders a single <c>IReadOnlyList&lt;SyntheticService&gt;</c> into the
///     default operations each service exposes. Used by both the
///     CLI default and the integration-test helpers so the resulting
///     spans look identical regardless of how the generator was
///     invoked. Kept in <c>Models/</c> alongside
///     <see cref="SyntheticService" /> because it is a pure
///     projection over the model type — no scenario-specific
///     domain logic lives here.
/// </summary>
public static class DefaultServiceFactory
{
    /// <summary>
    ///     Builds the canonical <see cref="SyntheticService" /> list
    ///     for the supplied names — each service gets three HTTP-style
    ///     operations on the <c>/{service}/items</c> path.
    /// </summary>
    public static IReadOnlyList<SyntheticService> Materialise(IReadOnlyList<string> names)
    {
        var result = new List<SyntheticService>(names.Count);
        foreach (var name in names)
        {
            result.Add(new SyntheticService
            {
                Name = name,
                Operations = new[]
                {
                    $"GET /{name}/items",
                    $"POST /{name}/items",
                    $"GET /{name}/items/{{id}}",
                },
            });
        }
        return result;
    }
}

/// <summary>
///     Identifies which scenario shape the generator runs. Mapped
///     from <c>GeneratorOptions.Scenario</c> at construction time —
///     invalid values surface as <see cref="ArgumentException" /> via
///     <c>ToScenarioKind</c> below.
/// </summary>
public enum ScenarioKind
{
    /// <summary>Single root span per request, no children. Cheapest baseline.</summary>
    Simple,

    /// <summary>Root span with 2–3 child spans per request, simulating a fan-out of dependencies.</summary>
    Fanout,

    /// <summary>Root span compensating each child on failure — models long-running transaction-like work.</summary>
    Saga,
}

/// <summary>
///     String-to-enum extension for the CLI / integration-test surface
///     over the scenario enum. Kept <c>internal</c> because callers
///     depend on the typed enum (the wire format is the string only
///     at parse time).
/// </summary>
internal static class ScenarioKindExtensions
{
    /// <summary>
    ///     Parses a CLI-supplied scenario name into the strongly-typed
    ///     enum. Unknown values surface as <see cref="ArgumentException" />
    ///     so callers see the typed failure immediately.
    /// </summary>
    public static ScenarioKind ToScenarioKind(this string value)
    {
        return value switch
        {
            "simple" => ScenarioKind.Simple,
            "fanout" => ScenarioKind.Fanout,
            "saga" => ScenarioKind.Saga,
            _ => throw new ArgumentException(
                $"Unknown scenario '{value}'. Expected one of: simple, fanout, saga.",
                nameof(value)),
        };
    }
}

/// <summary>
///     Top-level dispatch from <see cref="ScenarioKind" /> to the file-static
///     scenario implementations. Each scenario lives in its own file under
///     <c>Scenarios/</c> and exposes a single
///     <see cref="Generator.RunAsync(GeneratorOptions, CancellationToken)" />
///     — compatible method signature, so the dispatcher is a thin switch.
/// </summary>
internal static class ScenarioDispatcher
{
    /// <summary>
    ///     Runs the supplied <paramref name="iteration" /> using the
    ///     scenario's span-shape on the supplied
    ///     <see cref="System.Diagnostics.ActivitySource" /> and
    ///     <see cref="ILogger" />, returning the fake "overall" outcome
    ///     for RED metrics counters.
    /// </summary>
    /// <returns><see langword="true" /> if the scenario completed successfully; <see langword="false" /> on simulated failure.</returns>
    public static Task<bool> ExecuteAsync(ScenarioKind kind, ScenarioIteration iteration, CancellationToken cancellationToken)
    {
        return kind switch
        {
            ScenarioKind.Simple => SimpleScenario.RunAsync(iteration, cancellationToken),
            ScenarioKind.Fanout => FanoutScenario.RunAsync(iteration, cancellationToken),
            ScenarioKind.Saga => SagaScenario.RunAsync(iteration, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }
}
