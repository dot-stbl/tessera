using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Tessera.Shared.Kernel.Configuration.Paths;

namespace Tessera.Shared.Kernel.Configuration.Source;

/// <summary>
///     Tessera composition-root extensions for the
///     <see cref="IConfigurationBuilder" /> pipeline. Wires the two-file TOML
///     stack: <c>tessera.toml</c> base plus optional <c>tessera.local.toml</c>
///     override (same idea as <c>appsettings.json</c> + local overrides).
/// </summary>
public static class TesseraConfigurationExtensions
{
    /// <summary>
    ///     Add Tessera's TOML configuration sources. Prefer the host
    ///     <paramref name="contentRoot" /> (project directory under
    ///     <c>dotnet run</c>) so developers do not depend on process cwd.
    /// </summary>
    /// <param name="builder">The configuration builder.</param>
    /// <param name="contentRoot">
    ///     Host content root; when null, falls back to
    ///     <see cref="TesseraConfigPaths.ResolveMainPath(string?)"/> without a root.
    /// </param>
    /// <returns>The same builder, for chaining.</returns>
    public static IConfigurationBuilder AddTesseraConfiguration(
        this IConfigurationBuilder builder,
        string? contentRoot = null)
    {
        var mainPath = TesseraConfigPaths.ResolveMainPath(contentRoot);
        var localPath = TesseraConfigPaths.ResolveLocalOverridePath(mainPath);

        builder.Add(new TomlConfigurationSource
        {
            Path = mainPath,
            Optional = true,
            ReloadOnChange = false,
        })
            .Add(new TomlConfigurationSource
            {
                Path = localPath,
                Optional = true,
                ReloadOnChange = false,
            });

        return builder;
    }

    /// <summary>
    ///     Convenience overload for <see cref="HostApplicationBuilder" />.
    ///     Uses <see cref="IHostEnvironment.ContentRootPath" /> so
    ///     <c>dotnet run --project Tessera.Host</c> loads
    ///     <c>src/host/Tessera.Host/tessera.toml</c> regardless of shell cwd.
    /// </summary>
    public static HostApplicationBuilder AddTesseraConfiguration(this HostApplicationBuilder builder)
    {
        builder.Configuration.AddTesseraConfiguration(builder.Environment.ContentRootPath);
        return builder;
    }
}
