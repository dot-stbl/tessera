using Tessera.Shared.Kernel.Identifiers;

namespace Tessera.Shared.Kernel.Domain.Traces;

/// <summary>
///     Lightweight trace summary without spans. Returned by GET /api/traces list endpoint.
/// </summary>
public sealed record TraceSummary(
    TraceId TraceId,
    string RootService,
    string RootOperation,
    long StartTime,
    long DurationMs,
    TraceStatus Status,
    int SpanCount,
    IReadOnlyList<string> Services);
