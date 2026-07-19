using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Tessera.Shared.Kernel.Configuration;

/// <summary>
///     Tessera composition-root extensions for the
///     <see cref="IConfigurationBuilder" /> pipeline. Wires the two-file TOML
///     stack: <c>tessera.toml</c> base plus optional <c>tessera.local.toml</c>
///     override, ahead of env-var sources so secrets stay in env while
///     non-secret config lives in version-controlled files.
/// </summary>
public static class TesseraConfigurationExtensions
{
    /// <summary>
    ///     Add Tessera's TOML configuration sources to the builder. Resolves
    ///     the main path via <see cref="TesseraConfigPaths.ResolveMainPath" />,
    ///     then chains the local-override file at <c>tessera.local.toml</c> in
    ///     the same directory. Both files are optional; missing files are
    ///     silently skipped.
    /// </summary>
    /// <param name="builder">The configuration builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IConfigurationBuilder AddTesseraConfiguration(this IConfigurationBuilder builder)
    {
        var mainPath = TesseraConfigPaths.ResolveMainPath();
        var localPath = TesseraConfigPaths.ResolveLocalOverridePath(mainPath);

        builder.Add(new TomlConfigurationSource
        {
            Path = mainPath,
            Optional = true,
            ReloadOnChange = false,
        });

        builder.Add(new TomlConfigurationSource
        {
            Path = localPath,
            Optional = true,
            ReloadOnChange = false,
        });

        return builder;
    }

    /// <summary>
    ///     Convenience overload that augments a <see cref="HostApplicationBuilder" />
    ///     with <see cref="AddTesseraConfiguration(IConfigurationBuilder)" /> before
    ///     any other sources. Call this as the first builder mutation so
    ///     defaults in code remain the lowest-precedence layer.
    /// </summary>
    public static HostApplicationBuilder AddTesseraConfiguration(this HostApplicationBuilder builder)
    {
        builder.Configuration.AddTesseraConfiguration();
        return builder;
    }
}