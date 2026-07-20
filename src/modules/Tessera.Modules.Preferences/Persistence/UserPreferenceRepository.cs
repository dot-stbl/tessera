using System.Linq.Expressions;
using Tessera.Modules.Preferences.Application;
using Tessera.Modules.Preferences.Persistence.Specifications;
using Tessera.Shared.Kernel.Persistence;

namespace Tessera.Modules.Preferences.Persistence;

/// <summary>
///     Per-aggregate <see cref="Repository{TEntity}" /> subclass for
///     <see cref="UserPreference" />. Adds module-specific helpers —
///     <c>GetByUserKeyAsync</c>, <c>UpsertAsync</c>,
///     <c>SoftDeleteAsync</c> — that compose
///     <see cref="UserPreferenceSpecs" /> with the standard
///     <c>Repository&lt;T&gt;</c> surface (per
///     <c>repository-spec.md</c>).
/// </summary>
/// <param name="dbContext"></param>
public sealed class UserPreferenceRepository(PreferencesDbContext dbContext)
    : Repository<UserPreference>(dbContext)
{
    /// <summary>
    ///     Fetch a single preference by composite key
    ///     (<paramref name="userId" />, <paramref name="key" />).
    ///     Returns <c>null</c> when no active row matches.
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="key"></param>
    /// <param name="cancellationToken"></param>
    public Task<UserPreference?> GetByUserKeyAsync(
        string userId,
        string key,
        CancellationToken cancellationToken = default)
    {
        return FirstOrDefaultAsync(UserPreferenceSpecs.ByUserKey(userId, key), cancellationToken);
    }

    /// <summary>
    ///     Insert-or-update by composite key. MVP-01 uses
    ///     <c>FindAsync</c> + <c>SaveChanges</c> (acceptable for a
    ///     write-light table); set-based <c>ExecuteUpdate</c>
    ///     replaces this when the throughput grows.
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="key"></param>
    /// <param name="valueJson"></param>
    /// <param name="clock"></param>
    /// <param name="cancellationToken"></param>
    public async Task<UserPreference> UpsertAsync(
        string userId,
        string key,
        string valueJson,
        TimeProvider clock,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(clock);

        var existing = await GetByUserKeyAsync(userId, key, cancellationToken);
        if (existing is not null)
        {
            existing.ValueJson = valueJson;
            existing.UpdatedAt = clock.GetUtcNow();
            _ = await DbContext.SaveChangesAsync(cancellationToken);
            return existing;
        }

        var now = clock.GetUtcNow();
        var entity = new UserPreference
        {
            Id = "prf_" + Guid.NewGuid().ToString("N"),
            UserId = userId,
            TenantId = UserPreference.DefaultTenantId,
            Key = key,
            ValueJson = valueJson,
            CreatedAt = now,
            UpdatedAt = now,
        };

        return await AddAsync(entity, cancellationToken);
    }

    /// <summary>
    ///     Soft-delete by composite key. Sets <see cref="UserPreference.DeletedAt" />
    ///     to the current UTC time via <c>ExecuteUpdate</c> — single
    ///     SQL UPDATE, no round-trip per row.
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="key"></param>
    /// <param name="clock"></param>
    /// <param name="cancellationToken"></param>
    public Task<int> SoftDeleteAsync(
        string userId,
        string key,
        TimeProvider clock,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(clock);

        var now = clock.GetUtcNow();
        var parameter = Expression.Parameter(
            typeof(UserPreference), "preference");
        var body = Expression.Constant(
            now, typeof(DateTimeOffset?));
        var nowExpression = Expression.Lambda<
            Func<UserPreference, DateTimeOffset?>>(body, parameter);

        return ExecuteUpdateAsync(
            UserPreferenceSpecs.ByUserKey(userId, key),
            setters => setters.SetProperty(preference => preference.DeletedAt, nowExpression),
            cancellationToken);
    }
}
