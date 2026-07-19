namespace Tessera.Shared.Kernel.Configuration.Paths;

/// <summary>
///     Filesystem-anchored lookup for Tessera's TOML configuration files. The
///     resolution order is fixed per <c>.agents/docs/architecture/config-format.md</c>
///     — first non-empty hit wins.
/// </summary>
public static class TesseraConfigPaths
{
    /// <summary>Environment variable name for an explicit config-path override.</summary>
    public const string EnvOverride = "TESSERA_CONFIG";

    /// <summary>Main config file name (committed to source).</summary>
    public const string MainFileName = "tessera.toml";

    /// <summary>Local-override config file name (gitignored; env-specific overrides).</summary>
    public const string LocalFileName = "tessera.local.toml";

    /// <summary>Standard Linux system config directory (production-style).</summary>
    public const string EtcDirectory = "/etc/tessera";

    private const string XdgConfigHome = "XDG_CONFIG_HOME";

    /// <summary>
    ///     Resolve the absolute path of the main <c>tessera.toml</c> to load.
    ///     First non-empty hit wins:
    ///     <list type="number">
    ///         <item><c>TESSERA_CONFIG</c> env var (explicit override).</item>
    ///         <item><c>/etc/tessera/tessera.toml</c> (production Linux).</item>
    ///         <item>
    ///             <c>$XDG_CONFIG_HOME/tessera/tessera.toml</c> falling back to
    ///             <c>~/.config/tessera/tessera.toml</c> (user-level).
    ///         </item>
    ///         <item>
    ///             <c>{cwd}/tessera.toml</c> (dev) — always returned even if
    ///             the file does not exist, so a missing file is the caller's
    ///             problem to surface.
    ///         </item>
    ///     </list>
    /// </summary>
    public static string ResolveMainPath()
    {
        var envOverride = Environment.GetEnvironmentVariable(EnvOverride);
        if (!string.IsNullOrWhiteSpace(envOverride))
        {
            return envOverride;
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