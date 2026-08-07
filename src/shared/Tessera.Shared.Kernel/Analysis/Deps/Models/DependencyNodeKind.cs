namespace Tessera.Shared.Kernel.Analysis.Deps.Models;

/// <summary>
///     Role of a node on a per-trace dependency mini-map.
/// </summary>
public enum DependencyNodeKind
{
    /// <summary>Instrumented service identified by <c>service.name</c>.</summary>
    Service = 0,

    /// <summary>Synthetic database peer inferred from <c>db.system</c>.</summary>
    Database = 1,

    /// <summary>Synthetic external peer inferred from <c>peer.service</c> / <c>server.address</c>.</summary>
    External = 2,
}
