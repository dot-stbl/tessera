namespace Tessera.Shared.Kernel.Configuration.Layout;

/// <summary>
///     Linux / Unix filesystem layout following the XDG Base Directory
///     spec + traditional FHS-style system dirs. Used as the production
///     layout for Linux / Docker deployments; <see cref="FileSystemLayoutProvider" />
///     picks this implementation when <see cref="OperatingSystem.IsLinux()" />
///     returns <c>true</c>.
/// </summary>
/// <remarks>
///     <para>
///         Resolution order for each directory (first non-empty hit wins):
///     </para>
///     <list type="bullet">
///         <item><b>Config:</b> <c>TESSERA_CONFIG_DIR</c> env → <c>/etc/tessera/</c>
///             (system) → <c>$XDG_CONFIG_HOME/tessera</c> → <c>~/.config/tessera</c>.</item>
///         <item><b>Data:</b> <c>TESSERA_DATA_PATH</c> env →
///             <c>$XDG_DATA_HOME/tessera</c> → <c>~/.local/share/tessera</c>.</item>
///         <item><b>State:</b> data-dir/state/.</item>
///         <item><b>Cache:</b> <c>$XDG_CACHE_HOME/tessera</c> → <c>~/.cache/tessera</c>.</item>
///         <item><b>Logs:</b> <c>$TESSERA_LOG_DIR</c> env → <c>/var/log/tessera</c>
///             (system) → <c>$XDG_STATE_HOME/tessera</c> (user dev).</item>
///         <item><b>Temp:</b> <c>/tmp</c> (OS default).</item>
///     </list>
/// </remarks>
public sealed class LinuxFileSystemLayout : IFileSystemLayout
{
    /// <summary>Environment variable name for explicit config-dir override.</summary>
    public const string ConfigOverrideEnv = "TESSERA_CONFIG_DIR";

    /// <summary>Environment variable name for explicit data-root override.</summary>
    public const string DataOverrideEnv = "TESSERA_DATA_PATH";

    /// <summary>Environment variable name for explicit log-dir override.</summary>
    public const string LogOverrideEnv = "TESSERA_LOG_DIR";

    /// <summary>System-wide config dir (FHS convention).</summary>
    public const string SystemConfigDir = "/etc/tessera";

    /// <summary>System-wide data dir (FHS convention).</summary>
    public const string SystemDataDir = "/var/lib/tessera";

    /// <summary>System-wide log dir (FHS convention).</summary>
    public const string SystemLogDir = "/var/log/tessera";

    /// <summary>System-wide cache dir (FHS convention).</summary>
    public const string SystemCacheDir = "/var/cache/tessera";

    /// <summary>Application name for XDG-style directory naming.</summary>
    public const string AppName = "tessera";

    /// <inheritdoc />
    public DirectoryInfo ConfigDirectory { get; } = ResolveConfigDirectory();

    /// <inheritdoc />
    public DirectoryInfo DataDirectory { get; } = ResolveDataDirectory();

    /// <inheritdoc />
    public DirectoryInfo StateDirectory => new(Path.Combine(DataDirectory.FullName, "state"));

    /// <inheritdoc />
    public DirectoryInfo CacheDirectory { get; } = ResolveCacheDirectory();

    /// <inheritdoc />
    public DirectoryInfo LogDirectory { get; } = ResolveLogDirectory();

    /// <inheritdoc />
    public DirectoryInfo TempDirectory { get; } = new(Path.GetTempPath());

    /// <summary>
    ///     Create all directories if missing. Idempotent; safe to call
    ///     on every boot. Unix file mode 0700 for data/state — secrets
    ///     may live in these dirs (SQLite journal, token references).
    /// </summary>
    public void EnsureLayout()
    {
        ConfigDirectory.Create();
        DataDirectory.Create();
        StateDirectory.Create();
        CacheDirectory.Create();
        LogDirectory.Create();
        // TempDirectory is the OS temp root — already exists.

        ApplyUnixMode(DataDirectory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        ApplyUnixMode(StateDirectory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    private static DirectoryInfo ResolveConfigDirectory()
    {
        var envOverride = Environment.GetEnvironmentVariable(ConfigOverrideEnv);
        if (!string.IsNullOrWhiteSpace(envOverride))
        {
            return new DirectoryInfo(envOverride);
        }

        if (Directory.Exists(SystemConfigDir))
        {
            return new DirectoryInfo(SystemConfigDir);
        }

        var xdgRoot = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        var xdgBase = string.IsNullOrWhiteSpace(xdgRoot)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", AppName)
            : Path.Combine(xdgRoot, AppName);
        return new DirectoryInfo(xdgBase);
    }

    private static DirectoryInfo ResolveDataDirectory()
    {
        var envOverride = Environment.GetEnvironmentVariable(DataOverrideEnv);
        if (!string.IsNullOrWhiteSpace(envOverride))
        {
            return new DirectoryInfo(envOverride);
        }

        var xdgRoot = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (!string.IsNullOrWhiteSpace(xdgRoot))
        {
            return new DirectoryInfo(Path.Combine(xdgRoot, AppName));
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return new DirectoryInfo(Path.Combine(home, ".local", "share", AppName));
    }

    private static DirectoryInfo ResolveCacheDirectory()
    {
        var xdgRoot = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
        if (!string.IsNullOrWhiteSpace(xdgRoot))
        {
            return new DirectoryInfo(Path.Combine(xdgRoot, AppName));
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return new DirectoryInfo(Path.Combine(home, ".cache", AppName));
    }

    private static DirectoryInfo ResolveLogDirectory()
    {
        var envOverride = Environment.GetEnvironmentVariable(LogOverrideEnv);
        if (!string.IsNullOrWhiteSpace(envOverride))
        {
            return new DirectoryInfo(envOverride);
        }

        if (Directory.Exists(SystemLogDir))
        {
            return new DirectoryInfo(SystemLogDir);
        }

        var xdgStateRoot = Environment.GetEnvironmentVariable("XDG_STATE_HOME");
        if (!string.IsNullOrWhiteSpace(xdgStateRoot))
        {
            return new DirectoryInfo(Path.Combine(xdgStateRoot, AppName));
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return new DirectoryInfo(Path.Combine(home, ".local", "state", AppName));
    }

    private static void ApplyUnixMode(DirectoryInfo directory, UnixFileMode mode)
    {
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            File.SetUnixFileMode(directory.FullName, mode);
        }
        // Windows inherits ACLs from parent — no-op.
    }
}
