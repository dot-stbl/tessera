using Tessera.Shared.Kernel.Configuration.Layout;

namespace Tessera.Shared.Kernel.Configuration.Paths;

/// <summary>
///     Filesystem-anchored lookup for Tessera's TOML configuration files.
///     The resolution order is fixed per
///     <c>.agents/docs/architecture/config-format.md</c> — first non-empty
///     hit wins. Per ADR-0001 Decision 7, Linux-specific paths delegate to
///     <see cref="LinuxFileSystemLayout" /> via <see cref="FileSystemLayoutProvider.Detect" />;
///     Windows-specific paths use <see cref="WindowsFileSystemLayout" /> (in
///     Phase 4a via DI, here a static fallback for the bootstrap path).
/// </summary>
public static class TesseraConfigPaths
{
    /// <summary>Environment variable name for an explicit config-path override.</summary>
    public const string EnvOverride = "TESSERA_CONFIG";

    /// <summary>Main config file name (committed to source).</summary>
    public const string MainFileName = "tessera.toml";

    /// <summary>
    ///     Local-override config file name (gitignored; env-specific overrides).
    /// </summary>
    public const string LocalFileName = "tessera.local.toml";

    /// <summary>
    ///     Standard Linux system config directory (production-style). Kept as a
    ///     <c>const</c> for backward-compat with the original MVP-01 lookup.
    ///     Windows paths come from the layout abstraction instead.
    /// </summary>
    public const string EtcDirectory = "/etc/tessera";

    private const string XdgConfigHome = "XDG_CONFIG_HOME";

    /// <summary>
    ///     Resolve the absolute path of the main <c>tessera.toml</c> to load.
    ///     First non-empty hit wins:
    ///     <list type="number">
    ///         <item><c>TESSERA_CONFIG</c> env var (explicit override).</item>
    ///         <item><c>{layout.ConfigDirectory}/tessera.toml</c> for the active
    ///             OS (Linux: /etc/tessera or XDG fallback; Windows:
    ///             %ProgramData% / %APPDATA% / XDG-equivalent).</item>
    ///         <item><c>{cwd}/tessera.toml</c> (dev) — always returned even if
    ///             the file does not exist, so a missing file is the caller's
    ///             problem to surface.</item>
    ///     </list>
    /// </summary>
    public static string ResolveMainPath()
    {
        var envOverride = Environment.GetEnvironmentVariable(EnvOverride);
        if (!string.IsNullOrWhiteSpace(envOverride))
        {
            return envOverride;
        }

        var layout = FileSystemLayoutProvider.Detect();
        var layoutPath = Path.Combine(layout.ConfigDirectory.FullName, MainFileName);
        if (File.Exists(layoutPath))
        {
            return layoutPath;
        }

        // Legacy /etc/tessera fallback — Linux-only. The LinuxFileSystemLayout
        // already resolves this internally so the File.Exists check is
        // redundant for Linux; keeping it as a transitional safety net during
        // the layout migration window.
        var etcPath = Path.Combine(EtcDirectory, MainFileName);
        if (File.Exists(etcPath))
        {
            return etcPath;
        }

        // XDG fallback for user dev (Linux/macOS path).
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
