namespace Tessera.Shared.Kernel.Analysis.Deps.Models;

/// <summary>
///     A vertex on a per-trace dependency graph (service or synthetic peer).
/// </summary>
/// <param name="Id">Stable node key (service name, or <c>db:…</c> / <c>external:…</c>).</param>
/// <param name="Name">Human-readable label for the mini-map.</param>
/// <param name="Kind">Service, Database, or External.</param>
public sealed record DependencyNode(
    string Id,
    string Name,
    DependencyNodeKind Kind);
