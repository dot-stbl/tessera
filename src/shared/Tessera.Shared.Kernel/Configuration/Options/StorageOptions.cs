namespace Tessera.Shared.Kernel.Configuration.Options;

/// <summary>
///     EF Core persistence settings. Bound from the <c>[storage]</c> TOML
///     table by the host composition root.
///     <para>
///         <see cref="ConnectionString" />, when supplied, is used verbatim
///         — operators opt in to a fully-qualified connection string (e.g.
///         <c>"Data Source=/var/lib/tessera/tessera.db;Cache=Shared"</c>) and
///         take responsibility for the underlying provider. When absent,
///         the host falls back to the SQLite file path under
///     <see cref="Layout.IFileSystemLayout.DataDirectory" />.
///     </para>
///     <para>
///         <see cref="Provider" /> selects the EF Core provider wired in
///         the host; MVP-01 ships with <c>"sqlite"</c> only. Postgres /
///         SqlServer land in later phases per ADR-0001 Decision 5.
///     </para>
///     <para>
///         <c>MigrateOnStart</c> toggles automatic schema application at
///         boot. Default <c>true</c> for single-node MVP-01 deployments;
///         disable for clusters where schema migration is owned by an
///         external operator (the migration file lives next to the
///         module's DbContext so a multi-node roll-out can be coordinated).
///     </para>
/// </summary>
public sealed class StorageOptions
{
    /// <summary>Configuration section name in <c>tessera.toml</c>.</summary>
    public const string SectionName = "storage";

    /// <summary>
    ///     Provider identifier. <c>"sqlite"</c> is the MVP-01 default and
    ///     the only one wired in the host composition root. Future
    ///     phases add <c>"postgres"</c> / <c>"sqlserver"</c> per
    ///     ADR-0001 D5.
    /// </summary>
    public string Provider { get; init; } = "sqlite";

    /// <summary>
    ///     Operator-supplied connection string. When non-empty, used
    ///     verbatim; the host does not interpret it. Empty (the
    ///     default) → the host synthesizes a connection string from
    ///     <see cref="FileName" /> + <see cref="Layout.IFileSystemLayout.DataDirectory" />.
    /// </summary>
    public string? ConnectionString { get; init; }

    /// <summary>
    ///     SQLite file name. Resolved against
    ///     <see cref="Layout.IFileSystemLayout.DataDirectory" /> when
    ///     <see cref="ConnectionString" /> is null. Default
    ///     <c>"tessera.db"</c>; operators may override to spin up
    ///     multiple Tessera instances side-by-side against the
    ///     same data directory.
    /// </summary>
    public string FileName { get; init; } = "tessera.db";

    /// <summary>
    ///     Apply pending EF Core migrations on host startup. Default
    ///     <c>true</c>; disable in cluster deployments where the
    ///     migration file is owned by a separate operator run.
    /// </summary>
    public bool MigrateOnStart { get; init; } = true;
}
