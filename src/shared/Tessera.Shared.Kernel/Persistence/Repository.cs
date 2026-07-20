using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace Tessera.Shared.Kernel.Persistence;

/// <summary>
///     Generic base for per-module repositories. Subclasses add module-
///     specific helpers (e.g.
///     <c>Task&lt;OrderDetail&gt; GetDetailAsync(OrderId, ct)</c>)
///     but the wire between DbContext and
///     <see cref="ISpecification{T, TResult}" /> lives here.
///     <para>
///         Reads apply <see cref="ISpecification{T, TResult}.Tracking" />
///         before <see cref="ISpecification{T, TResult}.Apply" /> so
///         projection-shaped queries (most reads per
///         <c>csharp/ef-core.md</c> §5) run no-tracking by default.
///         Writes call <see cref="ExecuteUpdateAsync{TResult}" />
///         (no per-row roundtrip) or push entity adds via
///         <see cref="AddAsync{TEntity}" />.
///     </para>
///     <para>
///         The class is <c>abstract</c> — per
///         <c>repository-spec.md</c>, shared holds only the base
///         primitives; subclasses live in the owning module's
///         <c>Persistence/</c> folder next to its DbContext.
///     </para>
/// </summary>
/// <typeparam name="TEntity">Aggregate root the repository governs.</typeparam>
public abstract class Repository<TEntity>(DbContext dbContext)
    where TEntity : class
{
    /// <summary>
    ///     Underlying <see cref="DbContext" />. Subclasses reach for
    ///     this when they need to assemble related entities; spec-based
    ///     reads should not.
    /// </summary>
    protected DbContext DbContext { get; } = dbContext;

    /// <summary>Set for the aggregate root. Always non-null after construction.</summary>
    protected DbSet<TEntity> Set => DbContext.Set<TEntity>();

    /// <summary>
    ///     Project a specification into <see cref="IQueryable{TResult}" />,
    ///     applying per-spec tracking semantics.
    /// </summary>
    protected IQueryable<TResult> Apply<TResult>(ISpecification<TEntity, TResult> specification)
    {
        var source = (IQueryable<TEntity>)Set;
        var prepared = specification.Tracking switch
        {
            TrackingBehavior.NoTracking => source.AsNoTracking(),
            TrackingBehavior.Tracking => source,
            _ => source.AsNoTracking(),
        };

        return specification.Apply(prepared);
    }

    /// <summary>
    ///     First-or-default read — returns <c>null</c> when no row
    ///     matches. Default <c>NoTracking</c> (per
    ///     <see cref="ISpecification{T, TResult}.Tracking" />):
    ///     projection-shaped read, no change tracking.
    /// </summary>
    public Task<TResult?> FirstOrDefaultAsync<TResult>(
        ISpecification<TEntity, TResult> specification,
        CancellationToken cancellationToken = default)
    {
        return Apply(specification).FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    ///     First-or-default read with <c>Tracking</c> override —
    ///     for write paths that look up a tracked entity to mutate
    ///     in place (insert-or-update, soft-delete with reference
    ///     to entity, etc.). Spec semantics still apply (Where /
    ///     OrderBy / Projection are honoured), but the returned
    ///     entity participates in change tracking.
    ///     <para>
    ///         Use only when you need to <c>SaveChangesAsync</c>
    ///         updates against the loaded entity. Otherwise
    ///         prefer <see cref="FirstOrDefaultAsync{TResult}" />
    ///         — the default <c>NoTracking</c> is the faster
    ///         read path per <c>csharp/ef-core.md</c> §5.
    ///     </para>
    /// </summary>
    public Task<TResult?> FirstOrDefaultTrackedAsync<TResult>(
        ISpecification<TEntity, TResult> specification,
        CancellationToken cancellationToken = default)
    {
        var source = (IQueryable<TEntity>)Set;
        return specification
            .Apply(source)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    ///     Materialize a list read. Caller picks the projection via the
    ///     specification's <c>TResult</c> type.
    /// </summary>
    public Task<List<TResult>> ListAsync<TResult>(
        ISpecification<TEntity, TResult> specification,
        CancellationToken cancellationToken = default)
    {
        return Apply(specification).ToListAsync(cancellationToken);
    }

    /// <summary>
    ///     Count rows matching a spec. Used by modules that need to
    ///     surface totals without materializing the full page.
    /// </summary>
    public Task<int> CountAsync<TResult>(
        ISpecification<TEntity, TResult> specification,
        CancellationToken cancellationToken = default)
    {
        return Apply(specification).CountAsync(cancellationToken);
    }

    /// <summary>
    ///     Add a tracked entity and persist. Use for new-aggregate
    ///     writes only — for set-based updates prefer
    ///     <see cref="ExecuteUpdateAsync{TResult}" />. Returns the
    ///     tracked entity so callers can re-read computed properties
    ///     (<c>CreatedAt</c>, etc.) after the insert.
    /// </summary>
    public async Task<TLocalEntity> AddAsync<TLocalEntity>(
        TLocalEntity entity,
        CancellationToken cancellationToken = default)
        where TLocalEntity : class
    {
        var entry = await DbContext.AddAsync(entity, cancellationToken);
        await DbContext.SaveChangesAsync(cancellationToken);
        return entry.Entity;
    }

    /// <summary>
    ///     Set-based update — generate SQL
    ///     <c>UPDATE … WHERE (specification) SET …</c> via
    ///     <see cref="Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ExecuteUpdateAsync{T}(IQueryable{T}, Action{UpdateSettersBuilder{T}}, CancellationToken)" />.
    ///     No per-row roundtrip. EF Core 10 introduces
    ///     <see cref="UpdateSettersBuilder{T}" /> in place of the
    ///     <c>SetPropertyCalls</c> lambda of earlier versions —
    ///     callers receive an <see cref="UpdateSettersBuilder{T}" />
    ///     and chain <c>SetProperty(...)</c> calls without
    ///     hand-rolling <see cref="System.Linq.Expressions.Expression" />.
    /// </summary>
    public Task<int> ExecuteUpdateAsync<TResult>(
        ISpecification<TEntity, TResult> specification,
        Action<UpdateSettersBuilder<TResult>> setters,
        CancellationToken cancellationToken = default)
    {
        return Apply(specification).ExecuteUpdateAsync(setters, cancellationToken);
    }

    /// <summary>
    ///     Set-based delete. Pair with the soft-delete
    ///     <see cref="FilterHelpers.ActiveOnly{T}()" /> filter to keep
    ///     rows with <c>deleted_at</c> as the only persisted state.
    /// </summary>
    public Task<int> ExecuteDeleteAsync<TResult>(
        ISpecification<TEntity, TResult> specification,
        CancellationToken cancellationToken = default)
    {
        return Apply(specification).ExecuteDeleteAsync(cancellationToken);
    }
}
