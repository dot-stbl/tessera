using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tessera.Modules.Preferences.Persistence;

namespace Tessera.Modules.Preferences.DependencyInjection;

/// <summary>
///     Wires <see cref="PreferencesDbContext" /> + per-aggregate
///     repositories. Modules are wired in <c>Tessera.Host</c>'s
///     composition root via <c>AddPreferencesModule()</c> (per
///     <c>csharp/project-deps-and-tests.md</c> §"modules → shared
///     only") — provider code in <c>Tessera.Providers.*</c> never
///     references this extension.
/// </summary>
public static class PreferencesModuleExtensions
{
    /// <summary>
    ///     Connection string is sourced from <c>[storage]/connection_string</c>
    ///     (or a default SQLite file in <c>IFileSystemLayout.DataDirectory</c>);
    ///     see <c>Tessera.Host/Program.cs</c> for the resolution helper.
    ///     <para>
    ///         Wraps the typed <c>Action&lt;DbContextOptionsBuilder&lt;PreferencesDbContext&gt;&gt;</c>
    ///         into a non-generic <c>Action&lt;DbContextOptionsBuilder&gt;</c> delegate so
    ///         the EF Core 10 <see cref="EntityFrameworkServiceCollectionExtensions.AddDbContext{TContext}(IServiceCollection, Action{DbContextOptionsBuilder}, ServiceLifetime, ServiceLifetime)" />
    ///         extension is callable — that overload doesn't accept a
    ///         strongly-typed builder in EF Core 10.
    ///     </para>
    /// </summary>
    /// <param name="services"></param>
    /// <param name="configureDb"></param>
    public static IServiceCollection AddPreferencesModule(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder<PreferencesDbContext>> configureDb)
    {
        ArgumentNullException.ThrowIfNull(configureDb);

        void Bridge(DbContextOptionsBuilder builder) => configureDb((DbContextOptionsBuilder<PreferencesDbContext>)builder);

        _ = services.AddDbContext<PreferencesDbContext>(Bridge);

        _ = services.AddScoped<UserPreferenceRepository>();

        return services;
    }
}
