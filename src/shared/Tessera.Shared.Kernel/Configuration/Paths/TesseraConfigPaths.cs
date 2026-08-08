using Tessera.Shared.Kernel.Configuration.Layout;

namespace Tessera.Shared.Kernel.Configuration.Paths;

/// <summary>
///     Filesystem-anchored lookup for Tessera's TOML configuration files.
///     Dev mirrors ASP.NET <c>appsettings.json</c>: files next to the host
///     content root (project directory under <c>dotnet run</c>). Production
///     still resolves OS layout paths when content-root files are absent.
/// </summary>
public static class TesseraConfigPaths
{
    /// <summary>Environment variable name for an explicit config-path override.</summary>
    public const string EnvOverride = "TESSERA_CONFIG";

    /// <summary>Main config file name (committed next to the host, like appsettings.json).</summary>
    public const string MainFileName = "tessera.toml";

    /// <summary>
    ///     Local-override config file name (gitignored; machine-specific
    ///     overrides — like user secrets / local appsettings).
    /// </summary>
    public const string LocalFileName = "tessera.local.toml";

    /// <summary>
    ///     Standard Linux system config directory (production-style).
    /// </summary>
    public const string EtcDirectory = "/etc/tessera";

    private const string XdgConfigHome = "XDG_CONFIG_HOME";

    /// <summary>
    ///     Resolve the absolute path of the main <c>tessera.toml</c> to load.
    ///     First non-empty hit wins:
    ///     <list type="number">
    ///         <item><c>TESSERA_CONFIG</c> env var (explicit file path).</item>
    ///         <item>
    ///             <c>{contentRoot}/tessera.toml</c> when
    ///             <paramref name="contentRoot" /> is set and the file exists
    ///             (dev: host project directory under <c>dotnet run</c>).
    ///         </item>
    ///         <item>OS layout / <c>/etc/tessera</c> / XDG when those files exist.</item>
    ///         <item>
    ///             <c>{contentRoot}/tessera.toml</c> or <c>{cwd}/tessera.toml</c>
    ///             even if missing — optional load still points at a stable path.
    ///         </item>
    ///     </list>
    /// </summary>
    /// <param name="contentRoot">
    ///     Host content root (<see cref="Microsoft.Extensions.Hosting.IHostEnvironment.ContentRootPath" />).
    ///     Null falls back to legacy cwd-only resolution.
    /// </param>
    public static string ResolveMainPath(string? contentRoot = null)
    {
        var envOverride = Environment.GetEnvironmentVariable(EnvOverride);
        if (!string.IsNullOrWhiteSpace(envOverride))
        {
            return envOverride;
        }

        if (!string.IsNullOrWhiteSpace(contentRoot))
        {
            var contentRootMain = Path.Combine(contentRoot, MainFileName);
            if (File.Exists(contentRootMain))
            {
                return contentRootMain;
            }
        }

        var layout = FileSystemLayoutProvider.Detect();
        var layoutPath = Path.Combine(layout.ConfigDirectory.FullName, MainFileName);
        if (File.Exists(layoutPath))
        {
            return layoutPath;
        }

        var etcPath = Path.Combine(EtcDirectory, MainFileName);
        if (File.Exists(etcPath))
        {
            return etcPath;
        }

        var xdgRoot = Environment.GetEnvironmentVariable(XdgConfigHome);
        var xdgBase = string.IsNullOrWhiteSpace(xdgRoot)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".config",
                "tessera")
            : Path.Combine(xdgRoot, "tessera");
        var xdgPath = Path.Combine(xdgBase, MainFileName);
        if (File.Exists(xdgPath))
        {
            return xdgPath;
        }

        if (!string.IsNullOrWhiteSpace(contentRoot))
        {
            return Path.Combine(contentRoot, MainFileName);
        }

        return Path.Combine(Directory.GetCurrentDirectory(), MainFileName);
    }

    /// <summary>
    ///     Resolve the local-override file path. Always <c>tessera.local.toml</c>
    ///     in the same directory as the main file — the same loader picks it up
    ///     after the main so later keys win.
    /// </summary>
    public static string ResolveLocalOverridePath(string mainPath)
    {
        var directory = Path.GetDirectoryName(mainPath);
        return Path.Combine(directory ?? ".", LocalFileName);
    }
}
