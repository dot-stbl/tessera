namespace Tessera.Shared.Kernel.Domain.Traces;

using Tessera.Shared.Kernel.Domain.Spans;
using Tessera.Shared.Kernel.Identifiers;

/// <summary>
/// Full trace with reconstructed span tree. Returned by GET /api/traces/{traceId}.
/// </summary>
public sealed record TraceDetail(
    TraceId TraceId,
    string RootService,
    string RootOperation,
    long StartTime,
    long DurationMs,
    TraceStatus Status,
    IReadOnlyList<Span> Spans);