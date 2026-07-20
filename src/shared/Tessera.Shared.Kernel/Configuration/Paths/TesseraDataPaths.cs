namespace Tessera.Shared.Kernel.Configuration.Paths;

/// <summary>
///     Filesystem-anchored lookup for Tessera's runtime data root
///     — the directory tree under which the host writes file-based
///     state (sessions, caches, per-user config, per-tenant data).
///     Single-tree convention: <c>~/etc/tessera/</c> on developer
///     hosts, <c>/var/lib/tessera/</c> inside the compose container.
/// </summary>
/// <remarks>
///     <para>
///         The host's file-based persistence model mirrors the
///         Linux-user convention: principals own subtrees, the
///         boundary is the filesystem. Each user file is a plain
///         text document (TOML by default) and can be inspected
///         with <c>cat</c>, edited with <c>$EDITOR</c>, diffed
///         against another user's file. No SQL, no ORM, no
///         migrations — the directory tree is the schema.
///     </para>
///     <para>
///         Boots-time layout is created if missing — the host never
///         asks the developer to <c>mkdir -p</c> on a fresh
///         checkout. <see cref="EnsureDataTree" /> is idempotent
///         and safe to call from <c>Program.cs</c> on every boot.
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

    private const string XdgDataHome = "XDG_DATA_HOME";

    /// <summary>
    ///     Resolve the absolute path of the data root. First non-empty hit wins:
    ///     <list type="number">
    ///         <item><c>TESSERA_DATA_ROOT</c> env var (explicit override).</item>
    ///         <item><c>$XDG_DATA_HOME/tessera</c> falling back to <c>~/.local/share/tessera</c> (XDG).</item>
    ///         <item><c>$HOME/etc/tessera</c> (Linux-user convention).</item>
    ///     </list>
    /// </summary>
    public static string ResolveDataRoot()
    {
        var envOverride = Environment.GetEnvironmentVariable(EnvOverride);
        if (!string.IsNullOrWhiteSpace(envOverride))
        {
            return envOverride;
        }

        var xdgRoot = Environment.GetEnvironmentVariable(XdgDataHome);
        if (!string.IsNullOrWhiteSpace(xdgRoot))
        {
            return Path.Combine(xdgRoot, "tessera");
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, "etc", "tessera");
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
