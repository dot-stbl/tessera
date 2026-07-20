using Tessera.Modules.Traces.Contracts;
using Tessera.Shared.Kernel.Domain.Logs;
using Tessera.Shared.Kernel.Domain.Traces;

namespace Tessera.Modules.Traces.Mapping;

/// <summary>
///     Traces-module domain → wire DTO mappings. The interface fronts the
///     generated Mapperly implementation so the endpoint depends on the
///     contract, not on the source-generated partial.
/// </summary>
public interface ITracesMapper
{
    /// <summary>
    ///     Combine a fetched trace + correlated logs into the single
    ///     <see cref="GetTraceResponse" /> the endpoint returns.
    /// </summary>
    public GetTraceResponse ToResponse(TraceDetail? trace, IReadOnlyList<LogEntry> correlatedLogs);
}
