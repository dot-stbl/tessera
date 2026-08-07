using Tessera.Shared.Kernel.Analysis.Deps.Models;

namespace Tessera.Shared.Kernel.Analysis.Deps;

/// <summary>
///     Per-trace dependency mini-map: unique nodes plus aggregated edges.
/// </summary>
/// <param name="Nodes">Deduped vertices referenced by edges (and empty when none).</param>
/// <param name="Edges">Directed edges with call/error counts, merged by (from, to).</param>
public sealed record DependencyGraph(
    IReadOnlyList<DependencyNode> Nodes,
    IReadOnlyList<DependencyEdge> Edges);
