namespace Tessera.Shared.Kernel.Configuration.Layout;

/// <summary>
///     Resolves the canonical filesystem locations Tessera uses for
///     configuration, runtime data, transient state, cached artefacts,
///     logs, and temp scratch. Two implementations live alongside
///     this interface: <see cref="LinuxFileSystemLayout" /> (system
///     /etc/, /var/lib/, /var/log/, /var/cache/ + XDG fallback for
///     user dev) and <see cref="WindowsFileSystemLayout" /> (the
///     %ProgramData% / %LOCALAPPDATA% / %APPDATA% family + cwd-relative
///     fallback).
///     <para>
///         Auto-detection via <see cref="OperatingSystem.IsLinux()" />
///         and <see cref="OperatingSystem.IsWindows()" /> in
///         <see cref="FileSystemLayoutProvider" />. Override via
///         <c>[fs_layout]/provider</c> in <c>tessera.toml</c> or the
///         <c>TESSERA_FS_LAYOUT_PROVIDER</c> env var. Per ADR-0001
///         Decision 7.
///     </para>
/// </summary>
public interface IFileSystemLayout
{
    /// <summary>Where config files live (tessera.toml + local override).</summary>
    public DirectoryInfo ConfigDirectory { get; }

    /// <summary>Where runtime data lives (DB, dashboards JSON).</summary>
    public DirectoryInfo DataDirectory { get; }

    /// <summary>Where transient state lives (sessions, locks).</summary>
    public DirectoryInfo StateDirectory { get; }

    /// <summary>Where cached data lives (HTTP caches, computed artifacts).</summary>
    public DirectoryInfo CacheDirectory { get; }

    /// <summary>Where log files live (rotated by external logrotate / Windows EventLog).</summary>
    public DirectoryInfo LogDirectory { get; }

    /// <summary>Temp scratch space (default: OS temp root).</summary>
    public DirectoryInfo TempDirectory { get; }

    /// <summary>
    ///     Create all directories if missing. Idempotent — safe to call
    ///     from <c>Program.cs</c> on every boot. Windows inherits ACLs
    ///     from parent; Unix variants create with 0700 for directories
    ///     that may contain secrets (data / state).
    /// </summary>
    public void EnsureLayout();
}
