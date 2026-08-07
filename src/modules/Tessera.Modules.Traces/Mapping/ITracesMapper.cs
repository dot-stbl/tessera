using Tessera.Modules.Traces.Contracts;
using Tessera.Shared.Kernel.Analysis;

namespace Tessera.Modules.Traces.Mapping;

/// <summary>
///     Traces-module domain → wire DTO mappings. The interface fronts the
///     generated Mapperly implementation so the endpoint depends on the
///     contract, not on the source-generated partial.
/// </summary>
public interface ITracesMapper
{
    /// <summary>
    ///     Project a domain <see cref="RequestView" /> into the wire
    ///     <see cref="GetTraceResponse" />.
    /// </summary>
    public GetTraceResponse ToResponse(RequestView view);
}
