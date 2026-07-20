using Tessera.Shared.Kernel.Configuration.Layout;

namespace Tessera.Shared.Kernel.Configuration.Paths;

/// <summary>
///     Filesystem-anchored lookup for Tessera's runtime data root
///     — the directory tree under which the host writes file-based
///     state (sessions, caches, per-user config, per-tenant data).
///     Per ADR-0001 Decision 7, the lookup delegates to
///     <see cref="IFileSystemLayout" /> via <see cref="FileSystemLayoutProvider.Detect" />.
/// </summary>
/// <remarks>
///     <para>
///         Boots-time layout is created if missing — the host never
///         asks the developer to <c>mkdir -p</c> on a fresh
///         checkout. <see cref="EnsureDataTree(string)" /> is idempotent
///         and safe to call from <c>Program.cs</c> on every boot.
///     </para>
///     <para>
///         The pre-Phase-3 <c>$HOME/etc/tessera</c> fallback (which mixed
///         system /etc with user dir) was removed per ADR-0001 Decision 7
///         consequences — Linux user-dev falls through to
///         <c>~/.local/share/tessera/</c> via the XDG layout, no more
///         system-path confusion.
///     </para>
/// </remarks>
public static class TesseraDataPaths
{
    /// <summary>Environment variable name for an explicit data-root override.</summary>
    public const string EnvOverride = "TESSERA_DATA_ROOT";

    /// <summary>Directory under the data root where live config files live.</summary>
    public const string ConfigDirectoryName = "config";

    /// <summary>Directory under the data root where transient runtime state lives.</summary>
    public const string StateDirectoryName = "state";

    /// <summary>Directory under the data root where multi-tenant data lives.</summary>
    public const string TenantsDirectoryName = "tenants";

    /// <summary>Single-tenant default — matches VictoriaOptions.Tenant "0".</summary>
    public const string DefaultTenant = "0";

    /// <summary>
    ///     Resolve the absolute path of the data root. First non-empty hit wins:
    ///     <list type="number">
    ///         <item><c>TESSERA_DATA_ROOT</c> env var (explicit override).</item>
    ///         <item><c>{layout.DataDirectory}</c> from <see cref="FileSystemLayoutProvider.Detect" />.</item>
    ///     </list>
    /// </summary>
    /// <remarks>
    ///     This used to fall back to <c>$XDG_DATA_HOME/tessera</c> →
    ///     <c>~/.local/share/tessera</c> → <c>~/etc/tessera</c>.
    ///     The third fallback (<c>~/etc/tessera</c>) was removed per
    ///     ADR-0001 Decision 7 — the layout abstraction handles XDG
    ///     resolution internally.
    /// </remarks>
    public static string ResolveDataRoot()
    {
        var envOverride = Environment.GetEnvironmentVariable(EnvOverride);
        if (!string.IsNullOrWhiteSpace(envOverride))
        {
            return envOverride;
        }

        return FileSystemLayoutProvider.Detect().DataDirectory.FullName;
    }

    /// <summary>
    ///     Create the canonical directory tree under
    ///     <paramref name="root" /> if missing. Idempotent; safe to
    ///     call on every boot.
    /// </summary>
    public static void EnsureDataTree(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, ConfigDirectoryName));
        Directory.CreateDirectory(Path.Combine(root, StateDirectoryName));
        Directory.CreateDirectory(Path.Combine(root, TenantsDirectoryName, DefaultTenant));
    }
}
