using Tessera.Modules.Traces.Contracts;
using Tessera.Shared.Kernel.Analysis;
using Tessera.Shared.Kernel.Analysis.Errors;

namespace Tessera.Modules.Traces.Mapping;

/// <summary>
///     Projection of <see cref="RequestView" /> → <see cref="GetTraceResponse" />,
///     including error fields from <see cref="ErrorAnalysis" />.
/// </summary>
public sealed class TracesMapper : ITracesMapper
{
    /// <summary>
    ///     Map domain request view to the wire response. Renames
    ///     <see cref="RequestView.Logs" /> →
    ///     <see cref="GetTraceResponse.CorrelatedLogs" /> for back-compat and
    ///     attaches errored-span count + exception summaries.
    /// </summary>
    public GetTraceResponse ToResponse(RequestView view)
    {
        var errors = ErrorAnalysis.CollectErrors(view.Trace);
        return new GetTraceResponse
        {
            Trace = view.Trace,
            CorrelatedLogs = view.Logs,
            Mode = view.Mode,
            Markers = view.Markers,
            ErroredSpanCount = errors.Count,
            Exceptions = TracesErrorMapping.ToExceptionSummaries(errors),
        };
    }
}
