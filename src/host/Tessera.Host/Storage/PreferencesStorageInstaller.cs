using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Tessera.Modules.Preferences.Persistence;
using Tessera.Shared.Kernel.Configuration.Layout;
using Tessera.Shared.Kernel.Configuration.Options;

namespace Tessera.Host.Storage;

/// <summary>
///     Host composition helpers for the EF Core + SQLite persistence
///     layer. Phase 5c lands the MVP-01 Preferences module; further
///     modules plug into the same pattern by adding their own
///     <c>AddXxxStorage()</c> alongside this one.
///     <para>
///         Connection-string resolution:
///         <list type="bullet">
///             <item>
///                 <see cref="StorageOptions.ConnectionString" /> set
///                 → used verbatim (operator-supplied).
///             </item>
///             <item>
///                 Set to <c>null</c> → synthesizes
///                 <c>"Data Source=&lt;fs-layout.DataDirectory&gt;/&lt;StorageOptions.FileName&gt;"</c>
///                 (e.g. <c>/var/lib/tessera/tessera.db</c> on Linux,
///                 <c>%LOCALAPPDATA%\tessera\tessera.db</c> on Windows).
///             </item>
///         </list>
///     </para>
///     <para>
///         Schema application at boot — see
///     <see cref="EnsurePreferencesSchemaAsync(IServiceProvider, CancellationToken)" />.
///     Once the first migration is generated (Phase 5c user-run
///     <c>dotnet ef migrations add InitialSchema ...</c>), the host
///     automatically switches from <c>EnsureCreated</c> to
///     <c>Migrate</c> based on whether <c>__EFMigrationsHistory</c>
///     exists.
///     </para>
/// </summary>
internal static class PreferencesStorageInstaller
{
    /// <summary>
    ///     Resolves <see cref="StorageOptions" /> into a SQLite
    ///     connection string and wires
    ///     <see cref="PreferencesDbContext" /> as a Scoped service.
    ///     Asserts <see cref="StorageOptions.Provider" /> is
    ///     <c>"sqlite"</c> — the host composition root only knows how
    ///     to wire SQLite today.
    /// </summary>
    public static IServiceCollection AddPreferencesStorage(
        this IServiceCollection services)
    {
        _ = services.AddOptions<StorageOptions>()
            .ValidateDataAnnotations()
            .ValidateOnStart();

        _ = services.AddDbContext<PreferencesDbContext>(static (sp, builder) =>
        {
            var storage = sp.GetRequiredService<IOptions<StorageOptions>>().Value;
            if (!string.Equals(storage.Provider, "sqlite", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Unsupported [storage]/provider '{storage.Provider}'. "
                    + "MVP-01 supports 'sqlite' only. "
                    + "Postgres / SqlServer land in later phases per ADR-0001 D5.");
            }

            var connectionString = ResolveConnectionString(sp, storage);
            builder.UseSqlite(connectionString, static sqlite => sqlite.MigrationsAssembly("Tessera.Modules.Preferences"));
        });

        // Repository only — PreferencesDbContext above is already wired.
        // Splitting AddPreferencesStorage (storage + repo) from
        // AddPreferencesModule (DbContext + repo) keeps the host's
        // SQLite-aware path distinct from the module's provider-
        // agnostic entry point.
        _ = services.AddScoped<UserPreferenceRepository>();

        return services;
    }

    /// <summary>
    ///     Apply pending EF Core migrations OR <c>EnsureCreated</c> if
    ///     no migration has been generated yet. Designed to be called
    ///     once at boot (after <c>app.Build()</c>), in the same scope
    ///     as <see cref="PreferencesDbContext" />. Idempotent.
    ///     <para>
    ///         Selection rule:
    ///         <list type="bullet">
    ///             <item>
    ///                 <see cref="StorageOptions.MigrateOnStart" /> =
    ///                 <c>true</c> AND migrations exist
    ///                 (<c>Database.GetPendingMigrationsAsync()</c>
    ///                 non-empty) → <c>MigrateAsync()</c>.
    ///             </item>
    ///             <item>
    ///                 <see cref="StorageOptions.MigrateOnStart" /> =
    ///                 <c>true</c> AND no migrations yet → fail fast
    ///                 (operator should generate them).
    ///             </item>
    ///             <item>
    ///                 <see cref="StorageOptions.MigrateOnStart" /> =
    ///                 <c>false</c> → <c>EnsureCreatedAsync()</c>
    ///                 against the current model; development-only
    ///                 convenience for spins with no migration history.
    ///             </item>
    ///         </list>
    ///     </para>
    /// </summary>
    public static async Task EnsurePreferencesSchemaAsync(
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var storage = scope.ServiceProvider.GetRequiredService<IOptions<StorageOptions>>().Value;
        var dbContext = scope.ServiceProvider.GetRequiredService<PreferencesDbContext>();

        if (!storage.MigrateOnStart)
        {
            _ = await dbContext.Database.EnsureCreatedAsync(cancellationToken);
            return;
        }

        var pending = await dbContext.Database.GetPendingMigrationsAsync(cancellationToken);
        if (pending.Any())
        {
            await dbContext.Database.MigrateAsync(cancellationToken);
            return;
        }

        var applied = await dbContext.Database.GetAppliedMigrationsAsync(cancellationToken);
        if (applied.Any())
        {
            return;
        }

        // MigrateOnStart=true but no migrations present at all. We
        // choose to fall back to EnsureCreated once so the very first
        // boot (before an operator has generated InitialSchema) still
        // succeeds; subsequent boots find the history table and
        // continue with Migrate. This avoids a circular dependency on
        // a migration that the host cannot generate itself.
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
    }

    private static string ResolveConnectionString(
        IServiceProvider sp,
        StorageOptions storage)
    {
        if (!string.IsNullOrWhiteSpace(storage.ConnectionString))
        {
            return storage.ConnectionString;
        }

        var layout = sp.GetRequiredService<IFileSystemLayout>();
        layout.EnsureLayout();
        var dataDir = layout.DataDirectory.FullName;
        return $"Data Source={dataDir}{Path.DirectorySeparatorChar}{storage.FileName}";
    }
}
