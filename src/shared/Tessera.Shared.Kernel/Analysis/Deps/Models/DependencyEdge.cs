namespace Tessera.Shared.Kernel.Analysis.Deps.Models;

/// <summary>
///     Directed call edge between two dependency nodes on one trace.
///     Counts are aggregated across matching CLIENT spans.
/// </summary>
/// <param name="FromId">Caller node id.</param>
/// <param name="ToId">Callee node id.</param>
/// <param name="CallCount">Number of CLIENT spans that produced this edge.</param>
/// <param name="ErrorCount">
///     Subset of those CLIENT spans with <c>Status == Error</c>
///     (error attribution uses the CLIENT span status only).
/// </param>
public sealed record DependencyEdge(
    string FromId,
    string ToId,
    int CallCount,
    int ErrorCount);
