namespace Tessera.Shared.Kernel.Configuration.Layout;

/// <summary>
///     Windows filesystem layout following the
///     <see cref="Environment.SpecialFolder" /> convention:
///     <list type="bullet">
///         <item><b>Config:</b> <c>%ProgramData%\tessera\config\</c> (machine-wide)
///             or <c>%APPDATA%\tessera\config\</c> (user-level) — picked by
///             whether the process runs elevated.</item>
///         <item><b>Data:</b> <c>%LOCALAPPDATA%\tessera\data\</c>.</item>
///         <item><b>State:</b> data-dir/state/.</item>
///         <item><b>Cache:</b> <c>%LOCALAPPDATA%\tessera\cache\</c>.</item>
///         <item><b>Logs:</b> <c>%LOCALAPPDATA%\tessera\logs\</c>.</item>
///         <item><b>Temp:</b> <c>%TEMP%</c> (OS default).</item>
///     </list>
///     <para>
///         Override via <c>TESSERA_CONFIG_DIR</c> / <c>TESSERA_DATA_PATH</c> /
///         <c>TESSERA_LOG_DIR</c> environment variables (case-insensitive on
///         Windows). Falls back to <c>%USERPROFILE%\tessera\config\</c> /
///         <c>%LOCALAPPDATA%\tessera\data\</c> when no override is set.
///     </para>
///     <para>
///         ACLs inherit from parent directory on Windows — no explicit
///         <see cref="System.IO.File.GetUnixFileMode(string)" /> call (the
///         equivalent Windows API is <c>SetAccessControl</c> and is not used
///         in MVP-01; operator-side ACL configuration handles multi-user
///         deploys).
///     </para>
/// </summary>
public sealed class WindowsFileSystemLayout : IFileSystemLayout
{
    /// <summary>Application name used as the subdirectory under each SpecialFolder.</summary>
    public const string AppName = "tessera";

    /// <summary>Subdirectory under the config root.</summary>
    public const string ConfigSubdir = "config";

    /// <summary>Subdirectory under each layout root.</summary>
    public const string DataSubdir = "data";

    /// <summary>Subdirectory under each layout root.</summary>
    public const string StateSubdir = "state";

    /// <summary>Subdirectory under each layout root.</summary>
    public const string CacheSubdir = "cache";

    /// <summary>Subdirectory under each layout root.</summary>
    public const string LogsSubdir = "logs";

    /// <summary>Environment variable name for explicit config-dir override.</summary>
    public const string ConfigOverrideEnv = "TESSERA_CONFIG_DIR";

    /// <summary>Environment variable name for explicit data-root override.</summary>
    public const string DataOverrideEnv = "TESSERA_DATA_PATH";

    /// <summary>Environment variable name for explicit log-dir override.</summary>
    public const string LogOverrideEnv = "TESSERA_LOG_DIR";

    /// <inheritdoc />
    public DirectoryInfo ConfigDirectory { get; } = ResolveConfigDirectory();

    /// <inheritdoc />
    public DirectoryInfo DataDirectory { get; } = ResolveDataDirectory();

    /// <inheritdoc />
    public DirectoryInfo StateDirectory => new(Path.Combine(DataDirectory.FullName, StateSubdir));

    /// <inheritdoc />
    public DirectoryInfo CacheDirectory { get; } = new(Path.Combine(ResolveLocalAppDataRoot(), AppName, CacheSubdir));

    /// <inheritdoc />
    public DirectoryInfo LogDirectory { get; } = ResolveLogDirectory();

    /// <inheritdoc />
    public DirectoryInfo TempDirectory { get; } = new(Path.GetTempPath());

    /// <summary>
    ///     Create all directories if missing. Idempotent; safe to call on every
    ///     boot. ACLs inherit from parent — operator configures multi-user
    ///     deploys via standard Windows ACL tooling.
    /// </summary>
    public void EnsureLayout()
    {
        ConfigDirectory.Create();
        DataDirectory.Create();
        StateDirectory.Create();
        CacheDirectory.Create();
        LogDirectory.Create();
        // TempDirectory is %TEMP%, already exists.
    }

    private static DirectoryInfo ResolveConfigDirectory()
    {
        var envOverride = Environment.GetEnvironmentVariable(ConfigOverrideEnv);
        if (!string.IsNullOrWhiteSpace(envOverride))
        {
            return new DirectoryInfo(envOverride);
        }

        // Machine-wide if ProgramData is writable; user-level otherwise.
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var programDataDir = Path.Combine(programData, AppName, ConfigSubdir);
        if (CanWrite(programData))
        {
            return new DirectoryInfo(programDataDir);
        }

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return new DirectoryInfo(Path.Combine(appData, AppName, ConfigSubdir));
    }

    private static DirectoryInfo ResolveDataDirectory()
    {
        var envOverride = Environment.GetEnvironmentVariable(DataOverrideEnv);
        if (!string.IsNullOrWhiteSpace(envOverride))
        {
            return new DirectoryInfo(envOverride);
        }

        var localAppData = ResolveLocalAppDataRoot();
        return new DirectoryInfo(Path.Combine(localAppData, AppName, DataSubdir));
    }

    private static DirectoryInfo ResolveLogDirectory()
    {
        var envOverride = Environment.GetEnvironmentVariable(LogOverrideEnv);
        if (!string.IsNullOrWhiteSpace(envOverride))
        {
            return new DirectoryInfo(envOverride);
        }

        var localAppData = ResolveLocalAppDataRoot();
        return new DirectoryInfo(Path.Combine(localAppData, AppName, LogsSubdir));
    }

    private static string ResolveLocalAppDataRoot()
    {
        return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    }

    private static bool CanWrite(string dir)
    {
        try
        {
            Directory.CreateDirectory(dir);
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
