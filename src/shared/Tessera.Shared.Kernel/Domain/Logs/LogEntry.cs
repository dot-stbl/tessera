using Tessera.Shared.Kernel.Identifiers;
using Tessera.Shared.Kernel.Time;

namespace Tessera.Shared.Kernel.Domain.Logs;

/// <summary>
///     A single log entry. May be correlated with a trace (<see cref="TraceId" />) and/or
///     span (<see cref="SpanId" />). <see cref="Fields" /> carries structured key-value
///     attributes attached at log emission time.
/// </summary>
public sealed record LogEntry(
    long Timestamp,
    LogLevel Level,
    string Service,
    TraceId? TraceId,
    SpanId? SpanId,
    string Message,
    IReadOnlyDictionary<string, string> Fields);
