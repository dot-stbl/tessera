namespace Tessera.Shared.Storage;

/// <summary>
///     Base for per-module specification factories. Subclasses expose
///     <see cref="Compose" /> plus a fluent API for callers (e.g.
///     <c>ById(id)</c>, <c>ByStatus(s)</c>, <c>All()</c>); the
///     <see cref="Repository{TEntity}" /> consumes the resulting
///     <see cref="ISpecification{T, TResult}" />. Implementations live
///     in <c>Persistence/&lt;Entity&gt;Specs.cs</c> next to the
///     repository they feed.
///     <para>
///         Factories are <c>internal</c> — they're implementation
///     detail of the module. Tests construct concrete
///     <see cref="Specification{T, TResult}" /> instances directly.
///     </para>
/// </summary>
public abstract class SpecificationFactory<T, TResult>
    where T : class
{
    /// <summary>
    ///     Build the canonical "all rows of this shape" specification.
    ///     Most repositories expose this as the unfiltered read
    ///     (<c>filters apply per-caller</c> pattern).
    /// </summary>
    public abstract ISpecification<T, TResult> Compose();
}
