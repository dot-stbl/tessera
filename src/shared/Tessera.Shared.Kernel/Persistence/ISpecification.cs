namespace Tessera.Shared.Kernel.Persistence;

/// <summary>
///     Query-criteria contract for <see cref="Repository{TEntity}" />.
///     Specifications are immutable, composable, and side-effect free:
///     <see cref="Apply" /> transforms an <see cref="IQueryable{T}" />
///     into the projected <see cref="IQueryable{TResult}" /> — typically
///     a <c>Where</c> plus optional <c>Include</c> / <c>OrderBy</c> /
///     <c>Select</c>. Implementations live next to the entity they
///     shape (see <c>repository-spec.md</c>).
///     <para>
///         Implement <see cref="Specification{T, TResult}" /> directly
///         for tests; production concrete implementations come from
///         <see cref="SpecificationFactory{T, TResult}" />.
///     </para>
/// </summary>
/// <typeparam name="T">Entity type the criteria filters.</typeparam>
/// <typeparam name="TResult">
///     Materialization shape. Most module specs map <typeparamref name="T" />
///     to itself (1:1 reads). Aggregate-root projections
///     (<typeparamref name="T" /> → <c>OrderDetail</c>) use the broader type.
/// </typeparam>
public interface ISpecification<T, TResult>
    where T : class
{
    /// <summary>
    ///     Apply this specification to a queryable source. Pure
    ///     function — must not capture the underlying
    ///     <see cref="Microsoft.EntityFrameworkCore.DbContext" /> or any
    ///     side-effecting state.
    /// </summary>
    public IQueryable<TResult> Apply(IQueryable<T> source);

    /// <summary>
    ///     Tracking override applied by the repository before
    ///     <see cref="Apply" />. Default <see cref="TrackingBehavior.NoTracking" /> —
    ///     appropriate for projection-shaped reads; only tracking specs
    ///     flip this to <see cref="TrackingBehavior.Tracking" />.
    /// </summary>
    public TrackingBehavior Tracking => TrackingBehavior.NoTracking;
}
