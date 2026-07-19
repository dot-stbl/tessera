using Tessera.Shared.Kernel.Domain.Logs;
using Tessera.Shared.Kernel.Domain.Traces;

namespace Tessera.Modules.Traces.Contracts;

/// <summary>
///     Wire shape of <c>GET /api/traces/{traceId}</c> — the trace detail plus
///     its correlated logs in a single response (Kibana Observability style).
/// </summary>
public sealed record GetTraceResponse
{
    /// <summary>The full trace with reconstructed span tree, or null when the trace is not found.</summary>
    public TraceDetail? Trace { get; init; }

    /// <summary>Logs that share the trace's <c>traceId</c> field, ordered by timestamp.</summary>
    public required IReadOnlyList<LogEntry> CorrelatedLogs { get; init; }
}