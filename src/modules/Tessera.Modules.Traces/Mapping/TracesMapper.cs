using Riok.Mapperly.Abstractions;
using Tessera.Modules.Traces.Contracts;
using Tessera.Shared.Kernel.Analysis;

namespace Tessera.Modules.Traces.Mapping;

/// <summary>
///     Source-generated Mapperly projection of <see cref="RequestView" /> →
///     <see cref="GetTraceResponse" />.
/// </summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public sealed partial class TracesMapper : ITracesMapper
{
    /// <summary>
    ///     Map domain request view to the wire response. Renames
    ///     <see cref="RequestView.Logs" /> →
    ///     <see cref="GetTraceResponse.CorrelatedLogs" /> for back-compat.
    /// </summary>
    [MapProperty(nameof(RequestView.Logs), nameof(GetTraceResponse.CorrelatedLogs))]
    public partial GetTraceResponse ToResponse(RequestView view);
}
