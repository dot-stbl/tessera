namespace Tessera.Shared.Storage;

/// <summary>
///     Default <see cref="ISpecification{T, TResult}" /> implementation.
///     Implementations live in per-module
///     <c>Specifications/&lt;Entity&gt;Specs.cs</c>; they inherit this
///     base to gain tracking semantics for free. Concrete
///     specifications always come from the
///     <see cref="SpecificationFactory{T, TResult}" /> of the same
///     module — direct instantiation is reserved for tests.
/// </summary>
public abstract class Specification<T, TResult> : ISpecification<T, TResult>
    where T : class
{
    /// <inheritdoc />
    public TrackingBehavior Tracking => TrackingBehavior.NoTracking;

    /// <inheritdoc />
    public abstract IQueryable<TResult> Apply(IQueryable<T> source);
}
