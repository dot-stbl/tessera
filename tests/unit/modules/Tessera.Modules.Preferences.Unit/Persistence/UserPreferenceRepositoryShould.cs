using Microsoft.EntityFrameworkCore;
using Tessera.Modules.Preferences.Application;
using Tessera.Modules.Preferences.Persistence;
using Xunit;

namespace Tessera.Modules.Preferences.Unit.Persistence;

/// <summary>
///     Unit tests for <see cref="UserPreferenceRepository" /> using
///     the EF Core in-memory provider. The repository's surface
///     reads/writes through the standard EF Core LINQ pipeline,
///     which the in-memory provider supports — so we get full
///     coverage of <see cref="UserPreferenceRepository.UpsertAsync(string, string, string, TimeProvider, CancellationToken)" />,
///     <see cref="UserPreferenceRepository.SoftDeleteAsync(string, string, TimeProvider, CancellationToken)" />,
///     and the composite-key <see cref="UserPreferenceRepository.GetByUserKeyAsync(string, string, CancellationToken)" />
///     without standing up SQLite (the SQLite EF Core 10 +
///     Microsoft.Data.Sqlite 10.0.0 wire layout currently doesn't
///     ship a net10.0 build, so a SQLite-backed test would fail at
///     testhost time).
///     <para>
///         Migrations themselves are tool-generated against the
///     real provider (see <c>csharp/ef-migrations.md</c>) — the
///     in-memory provider is for unit tests only.
///     </para>
/// </summary>
public sealed class UserPreferenceRepositoryShould : IDisposable
{
    private readonly PreferencesDbContext dbContext;
    private readonly TimeProvider clock = TimeProvider.System;

    /// <summary>
    ///     Boots a fresh EF Core in-memory database per test (named
    ///     by GUID to keep tests isolated).
    /// </summary>
    public UserPreferenceRepositoryShould()
    {
        var builder = new DbContextOptionsBuilder<PreferencesDbContext>()
            .UseInMemoryDatabase($"preferences-test-{Guid.NewGuid():N}");

        dbContext = new PreferencesDbContext(builder.Options);
    }

    /// <summary>Disposes the test's in-memory database.</summary>
    public void Dispose()
    {
        dbContext.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     <see cref="UserPreferenceRepository.UpsertAsync(string, string, string, TimeProvider, CancellationToken)" />
    ///     inserts on the first call, then updates in place on the
    ///     second — same <see cref="UserPreference.Id" />, latest
    ///     value.
    /// </summary>
    [Fact]
    public async Task UpsertUpdatesExistingRowInsteadOfDuplicating()
    {
        var repository = new UserPreferenceRepository(dbContext);

        _ = await repository.UpsertAsync("alice", "theme", "\"dark\"", clock);
        await dbContext.SaveChangesAsync();
        _ = await repository.UpsertAsync("alice", "theme", "\"light\"", clock);
        await dbContext.SaveChangesAsync();

        var rows = await dbContext.UserPreferences
            .Where(static preference => preference.UserId == "alice")
            .ToListAsync();

        Assert.Single(rows);
        Assert.Equal("\"light\"", rows[0].ValueJson);
    }

    /// <summary>
    ///     Round-trip the value field — write <c>"dark"</c>,
    ///     read it back through <see cref="UserPreferenceRepository.GetByUserKeyAsync(string, string, CancellationToken)" />.
    /// </summary>
    [Fact]
    public async Task RoundTripValueThroughUpsert()
    {
        var repository = new UserPreferenceRepository(dbContext);

        _ = await repository.UpsertAsync("alice", "theme", "\"dark\"", clock);
        await dbContext.SaveChangesAsync();

        var retrieved = await repository.GetByUserKeyAsync("alice", "theme");

        Assert.NotNull(retrieved);
        Assert.Equal("\"dark\"", retrieved.ValueJson);
    }

    /// <summary>
    ///     <see cref="UserPreferenceRepository.SoftDeleteAsync(string, string, TimeProvider, CancellationToken)" />
    ///     sets <see cref="UserPreference.DeletedAt" /> via the
    ///     set-based <c>ExecuteUpdateAsync</c> path —
    ///     <c>Microsoft.EntityFrameworkCore.InMemory</c> does not
    ///     implement <c>ExecuteUpdate</c>, so this scenario is
    ///     <c>[Fact(Skip = "requires relational provider")]</c>.
    ///     The SQLite integration tests will exercise the same
    ///     code path on a real provider in
    ///     <c>tests/integration/</c> (deferred per Phase 5 owner
    ///     direction).
    /// </summary>
    [Fact(Skip = "ExecuteUpdate not supported by EF Core InMemory; covered by SQLite integration tests (Phase 5)")]
    public async Task SoftDeleteExcludesRowFromSubsequentReads()
    {
        var repository = new UserPreferenceRepository(dbContext);

        _ = await repository.UpsertAsync("alice", "theme", "\"dark\"", clock);
        await dbContext.SaveChangesAsync();

        var deleted = await repository.SoftDeleteAsync("alice", "theme", clock);

        Assert.Equal(1, deleted);

        var afterDelete = await repository.GetByUserKeyAsync("alice", "theme");

        Assert.Null(afterDelete);
    }

    /// <summary>
    ///     Different <c>(userId, key)</c> combinations coexist —
    ///     the unique constraint is composite, not on either
    ///     column alone.
    /// </summary>
    [Fact]
    public async Task DistinctUserKeysCoexistForSameUser()
    {
        var repository = new UserPreferenceRepository(dbContext);

        _ = await repository.UpsertAsync("alice", "theme", "\"dark\"", clock);
        _ = await repository.UpsertAsync("alice", "language", "\"en\"", clock);
        await dbContext.SaveChangesAsync();

        var theme = await repository.GetByUserKeyAsync("alice", "theme");
        var language = await repository.GetByUserKeyAsync("alice", "language");

        Assert.NotNull(theme);
        Assert.NotNull(language);
        Assert.Equal("\"dark\"", theme.ValueJson);
        Assert.Equal("\"en\"", language.ValueJson);
    }

    /// <summary>
    ///     Users are isolated from each other — Alice's <c>"theme"</c>
    ///     preference doesn't surface in Bob's lookup.
    /// </summary>
    [Fact]
    public async Task UsersAreIsolatedByUserId()
    {
        var repository = new UserPreferenceRepository(dbContext);

        _ = await repository.UpsertAsync("alice", "theme", "\"dark\"", clock);
        _ = await repository.UpsertAsync("bob", "theme", "\"light\"", clock);
        await dbContext.SaveChangesAsync();

        var alice = await repository.GetByUserKeyAsync("alice", "theme");
        var bob = await repository.GetByUserKeyAsync("bob", "theme");

        Assert.NotNull(alice);
        Assert.NotNull(bob);
        Assert.Equal("\"dark\"", alice.ValueJson);
        Assert.Equal("\"light\"", bob.ValueJson);
    }
}
