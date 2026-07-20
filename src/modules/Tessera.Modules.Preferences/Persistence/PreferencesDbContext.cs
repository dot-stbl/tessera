using Microsoft.EntityFrameworkCore;
using Tessera.Modules.Preferences.Application;

namespace Tessera.Modules.Preferences.Persistence;

/// <summary>
///     Per-module <see cref="DbContext" /> for Tessera.Modules.Preferences.
///     Carries the user-preferences aggregate — module schema
///     (<c>tessera</c>) keeps preferences separate from upstream
///     Victoria data and future module-owned tables.
///     <para>
///         Schema + table names come from the per-module
///     <see cref="Configurations.UserPreferenceConfiguration" /> —
///         not from <c>[Table]</c> data annotations on the entity
///         itself. The single <see cref="OnModelCreating" /> call
///         applies the configuration; future entities added to this
///         module get an additional <c>ApplyConfiguration</c> line.
///     </para>
/// </summary>
/// <param name="options"></param>
public sealed class PreferencesDbContext(DbContextOptions<PreferencesDbContext> options)
    : DbContext(options)
{
    /// <summary>Set carrying <see cref="UserPreference" /> rows.</summary>
    public DbSet<UserPreference> UserPreferences => Set<UserPreference>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        _ = modelBuilder.ApplyConfiguration(new Configurations.UserPreferenceConfiguration());
        base.OnModelCreating(modelBuilder);
    }
}
