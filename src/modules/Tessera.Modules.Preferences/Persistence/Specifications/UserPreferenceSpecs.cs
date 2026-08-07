using System.Linq.Expressions;
using Tessera.Modules.Preferences.Application;
using Tessera.Shared.Storage;

namespace Tessera.Modules.Preferences.Persistence.Specifications;

/// <summary>
///     Per-entity <see cref="SpecificationFactory{T, TResult}" /> for
///     <see cref="UserPreference" />. Per <c>repository-spec.md</c>, all
///     specs for one entity live in <c>&lt;Entity&gt;Specs.cs</c>
///     (not a folder of one-spec files).
///     <para>
///         All specifications apply the soft-delete predicate
///     (<see cref="FilterHelpers.ActiveOnly{T}" />) so callers don't
///     need to compose it on every read. Reads project to
///     <c>UserPreferenceSummary</c> / <c>UserPreferenceDetail</c>
///     through <c>csharp/mapping.md</c> when that
///     asymmetry surfaces; MVP-01 reads are 1:1.
///     </para>
/// </summary>
public sealed class UserPreferenceSpecs : SpecificationFactory<UserPreference, UserPreference>
{
    /// <inheritdoc />
    public override ISpecification<UserPreference, UserPreference> Compose()
    {
        return AllActive();
    }

    /// <summary>All non-deleted preferences for this user.</summary>
    public static ISpecification<UserPreference, UserPreference> AllActive()
    {
        return new AllActiveSpec();
    }

    /// <summary>Single (user, key) lookup — returns null when absent.</summary>
    /// <param name="userId"></param>
    /// <param name="key"></param>
    public static ISpecification<UserPreference, UserPreference> ByUserKey(
        string userId,
        string key)
    {
        return new ByUserKeySpec(userId, key);
    }

    /// <summary>All non-deleted preferences in the supplied tenant scope.</summary>
    /// <param name="tenantId"></param>
    public static ISpecification<UserPreference, UserPreference> InTenant(
        string tenantId)
    {
        return new InTenantSpec(tenantId);
    }

    private sealed class AllActiveSpec : Specification<UserPreference, UserPreference>
    {
        public override IQueryable<UserPreference> Apply(IQueryable<UserPreference> source)
        {
            return source.Where(FilterHelpers.ActiveOnly<UserPreference>())
                .OrderBy(static preference => preference.Key);
        }
    }

    private sealed class ByUserKeySpec : Specification<UserPreference, UserPreference>
    {
        private readonly string userId;
        private readonly string key;

        public ByUserKeySpec(string userId, string key)
        {
            ArgumentNullException.ThrowIfNull(userId);
            ArgumentNullException.ThrowIfNull(key);
            this.userId = userId;
            this.key = key;
        }

        public override IQueryable<UserPreference> Apply(IQueryable<UserPreference> source)
        {
            return source.Where(BuildPredicate());
        }

        private Expression<Func<UserPreference, bool>> BuildPredicate()
        {
            var userIdConstant = Expression.Constant(userId);
            var keyConstant = Expression.Constant(key);
            var parameter = Expression.Parameter(typeof(UserPreference), "preference");

            var userIdAccess = Expression.Property(parameter, nameof(UserPreference.UserId));
            var keyAccess = Expression.Property(parameter, nameof(UserPreference.Key));
            var deletedAccess = Expression.Property(parameter, nameof(UserPreference.DeletedAt));

            var body = Expression.AndAlso(
                Expression.Equal(userIdAccess, userIdConstant),
                Expression.AndAlso(
                    Expression.Equal(keyAccess, keyConstant),
                    Expression.Equal(deletedAccess, Expression.Constant(null, typeof(DateTimeOffset?)))));

            return Expression.Lambda<Func<UserPreference, bool>>(body, parameter);
        }
    }

    private sealed class InTenantSpec : Specification<UserPreference, UserPreference>
    {
        private readonly string tenantId;

        public InTenantSpec(string tenantId)
        {
            ArgumentNullException.ThrowIfNull(tenantId);
            this.tenantId = tenantId;
        }

        public override IQueryable<UserPreference> Apply(IQueryable<UserPreference> source)
        {
            return source
                .Where(FilterHelpers.ActiveOnly<UserPreference>())
                .Where(FilterHelpers.TenantScoped<UserPreference>(tenantId))
                .OrderBy(static preference => preference.UserId)
                .ThenBy(static preference => preference.Key);
        }
    }
}
