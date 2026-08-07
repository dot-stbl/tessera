namespace Tessera.Shared.Storage;

/// <summary>
///     Predicate expressions commonly composed into
///     <see cref="ISpecification{T, TResult}" /> implementations
///     (equality, "in", soft-delete filter, tenant-scope filter). Lives
///     as a file-static class so it doesn't turn into a repository
///     god-class; module specs compose these helpers + their own domain
///     criteria.
/// </summary>
public static class FilterHelpers
{
    /// <summary>
    ///     Standard soft-delete filter used by every per-module entity
    ///     that carries a <c>deleted_at</c> column. <c>null</c> means
    ///     "active row"; non-null means logically deleted and excluded
    ///     from reads by default.
    /// </summary>
    public static System.Linq.Expressions.Expression<
            System.Func<T, bool>> ActiveOnly<T>()
        where T : class
    {
        return static entity => !Microsoft.EntityFrameworkCore.EF.Property<System.DateTimeOffset?>(
            entity,
            "deleted_at")
            .HasValue;
    }

    /// <summary>
    ///     Tenant-scope filter. Single-tenant MVP-01 always returns
    ///     <c>true</c>; multi-tenant (ADR-0001 §11 stretch) replaces
    ///     the literal with <c>entity =&gt;
    ///     EF.Property&lt;string&gt;(entity, "tenant_id") ==
    ///     currentTenantId</c>. Kept abstract here so modules can
    ///     layer in the actual column.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///     The supplied <typeparamref name="T" /> does not expose a
    ///     public <c>TenantId</c> property — every per-module entity
    ///     that participates in tenant scoping must declare one.
    /// </exception>
    public static System.Linq.Expressions.Expression<
            System.Func<T, bool>> TenantScoped<T>(string tenantId)
        where T : class
    {
        var constant = System.Linq.Expressions.Expression.Constant(tenantId);
        var parameter = System.Linq.Expressions.Expression.Parameter(typeof(T), "entity");
        var property = System.Linq.Expressions.Expression.Property(
            parameter,
            typeof(T).GetProperty("TenantId") ?? throw new InvalidOperationException(
                "Tenant-scoped filter requires a public TenantId property on " + typeof(T).Name));

        var body = System.Linq.Expressions.Expression.Equal(property, constant);
        return System.Linq.Expressions.Expression.Lambda<
            System.Func<T, bool>>(body, parameter);
    }
}
