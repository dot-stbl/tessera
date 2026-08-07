using Tessera.Shared.Kernel.Analysis;
using Tessera.Shared.Kernel.Analysis.Deps;
using Tessera.Shared.Kernel.Domain.Logs;
using Tessera.Shared.Kernel.Domain.Traces;

namespace Tessera.Modules.Traces.Contracts;

/// <summary>
///     Wire shape of <c>GET /api/traces/{traceId}</c> — degradable request
///     view (spans + correlated logs + mode + markers).
/// </summary>
public sealed record GetTraceResponse
{
    /// <summary>The full trace with reconstructed span tree, or null when spans are absent (logs-only).</summary>
    public TraceDetail? Trace { get; init; }

    /// <summary>Logs that share the trace's <c>traceId</c> field, ordered by timestamp.</summary>
    public required IReadOnlyList<LogEntry> CorrelatedLogs { get; init; }

    /// <summary>
    ///     Degradation mode for FE banners:
    ///     <c>Full</c>, <c>SpansOnly</c>, <c>LogsOnly</c> (never <c>Empty</c> on 200).
    /// </summary>
    public required RequestViewMode Mode { get; init; }

    /// <summary>Timeline markers derived from correlated logs.</summary>
    public required IReadOnlyList<LogMarker> Markers { get; init; }

    /// <summary>
    ///     Count of spans with <see cref="Tessera.Shared.Kernel.Domain.Traces.TraceStatus.Error" />.
    ///     Zero when <see cref="Trace" /> is null or has no errored spans.
    /// </summary>
    public int ErroredSpanCount { get; init; }

    /// <summary>
    ///     Exception summaries for errored spans (type/message/span/service/operation).
    ///     Empty when no error spans or no exception events.
    /// </summary>
    public IReadOnlyList<TraceExceptionSummary> Exceptions { get; init; } = [];

    /// <summary>
    ///     Per-trace dependency mini-map derived from CLIENT spans.
    ///     Null when <see cref="Trace" /> is null (logs-only); empty graph when
    ///     spans exist but yield no dependency edges.
    /// </summary>
    public DependencyGraph? DependencyGraph { get; init; }
}
