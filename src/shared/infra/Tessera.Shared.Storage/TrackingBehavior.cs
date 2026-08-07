namespace Tessera.Shared.Storage;

/// <summary>
///     Read/write tracking semantics applied to a query built from an
///     <see cref="ISpecification{T, TResult}" />. The
///     <see cref="Repository{TEntity}" /> base applies this when
///     materializing queries.
///     <para>
///         Most module reads pass <see cref="NoTracking" /> — the result
///         is a projection that doesn't need change tracking
///         (per <c>csharp/ef-core.md</c> §5). Tracking is reserved for
///         write paths that depend on the change tracker.
///     </para>
/// </summary>
public enum TrackingBehavior
{
    /// <summary>Override the per-spec tracking value to force <c>NoTracking</c>.</summary>
    NoTracking = 0,

    /// <summary>Override the per-spec tracking value to keep the default EF change tracking.</summary>
    Tracking = 1,
}
