using Riok.Mapperly.Abstractions;
using Tessera.Modules.Traces.Contracts;
using Tessera.Shared.Kernel.Domain.Logs;
using Tessera.Shared.Kernel.Domain.Traces;

namespace Tessera.Modules.Traces.Mapping;

/// <summary>
///     Source-generated Mapperly projection of <see cref="TraceDetail" /> +
///     correlated logs → <see cref="GetTraceResponse" />.
/// </summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public sealed partial class TracesMapper : ITracesMapper
{
    /// <summary>
    ///     Combine a fetched trace + correlated logs into the
    ///     <see cref="GetTraceResponse" /> returned by the endpoint.
    ///     <see cref="MapPropertyFromSourceAttribute" /> on
    ///     <see cref="GetTraceResponse.Trace" /> tells Mapperly to assign the
    ///     full <paramref name="trace" /> object to the target property
    ///     instead of trying to map field-by-field (the target DTO only has
    ///     two properties — <c>Trace</c> + <c>CorrelatedLogs</c> — and shares
    ///     no field names with the source <see cref="TraceDetail" />).
    /// </summary>
    [MapPropertyFromSource(nameof(GetTraceResponse.Trace))]
    public partial GetTraceResponse ToResponse(TraceDetail? trace, IReadOnlyList<LogEntry> correlatedLogs);
}
