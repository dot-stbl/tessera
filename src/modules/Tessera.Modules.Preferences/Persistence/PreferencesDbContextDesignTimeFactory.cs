using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Tessera.Modules.Preferences.Persistence;

/// <summary>
///     Design-time <see cref="IDesignTimeDbContextFactory{TContext}" />
///     for <see cref="PreferencesDbContext" />.
///     <para>
///         Migrations are tool-generated only (per
///         <c>csharp/ef-migrations.md</c>, hand-written migrations
///         are banned). The factory never runs at request time;
///         Tessera.Host wires the real SQLite provider through
///         <c>PreferencesStorageInstaller</c>.
///     </para>
/// </summary>
public sealed class PreferencesDbContextDesignTimeFactory : IDesignTimeDbContextFactory<PreferencesDbContext>
{
    /// <inheritdoc />
    public PreferencesDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<PreferencesDbContext>()
            .UseSqlite(
                "Data Source=tessera-design.db",
                static sqlite => sqlite.MigrationsAssembly("Tessera.Modules.Preferences"));

        return new PreferencesDbContext(builder.Options);
    }
}
