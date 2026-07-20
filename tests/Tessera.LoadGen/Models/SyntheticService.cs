namespace Tessera.LoadGen.Models;

/// <summary>
///     Synthetic service shape used by <see cref="Generator" />. Each
///     instance carries the wire attributes that
///     <c>Tessera.Providers.Victoria</c> later reads back through the
///     <c>GET /api/v1/services</c> discovery endpoint, so the
///     load-generator output is realistic from the Tessera side of the wire.
/// </summary>
/// <remarks>
///     <para>
///         The OTel semantic-convention attributes
///         (<c>service.name</c>, <c>deployment.environment</c>) are
///         attached to the root <c>Activity</c> by
///         <see cref="Generator" /> — these properties stay focused
///         on the operations that the service exposes rather than
///         the resource attributes that OTel expects at the SDK
///         boundary.
///     </para>
///     <para>
///         <c>Operations</c> is the list of operation names the
///         generator will randomly pick from when emitting a synthetic
///         request. A realistic service exposes 3–6 operations; the
///         default payload uses HTTP-style verbs.
///     </para>
/// </remarks>
public sealed record SyntheticService
{
    /// <summary>The service identifier — emitted as <c>service.name</c> on the trace.</summary>
    public required string Name { get; init; }

    /// <summary>Operations this service exposes. Picked uniformly at random per request.</summary>
    public required IReadOnlyList<string> Operations { get; init; }
}
