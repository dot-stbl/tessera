using Tessera.Shared.Kernel.Domain.Logs;
using Tessera.Shared.Kernel.Identifiers;
using Tessera.Shared.Kernel.Pagination;
using Tessera.Shared.Kernel.Time;

namespace Tessera.Shared.Kernel.Providers.Logs;

/// <summary>
///     Abstraction over a log data source backend (VictoriaLogs, Loki, etc.).
/// </summary>
public interface ILogProvider
{
    /// <summary>
    ///     Query logs by filter. Returns a cursor-paginated page.
    /// </summary>
    public Task<Page<LogEntry>> QueryAsync(LogQuery query, CancellationToken ct);

    /// <summary>
    ///     List all logs correlated with a specific trace within a time range.
    ///     Used by <c>GetTraceHandler</c> to embed correlated logs in the
    ///     trace detail response (Kibana Observability style).
    /// </summary>
    public Task<IReadOnlyList<LogEntry>> ListByTraceAsync(TraceId traceId, TimeRange range, CancellationToken ct);
}
