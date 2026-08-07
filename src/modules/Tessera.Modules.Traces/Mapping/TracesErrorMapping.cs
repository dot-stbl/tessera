using Tessera.Modules.Traces.Contracts;
using Tessera.Shared.Kernel.Analysis.Errors;

namespace Tessera.Modules.Traces.Mapping;

/// <summary>
///     Pure projections from kernel <see cref="SpanError" /> to wire exception DTOs.
/// </summary>
internal static class TracesErrorMapping
{
    /// <summary>
    ///     Project collected span errors into request-view exception summaries
    ///     (stacktrace omitted on the list wire shape).
    /// </summary>
    public static IReadOnlyList<TraceExceptionSummary> ToExceptionSummaries(
        IReadOnlyList<SpanError> errors)
    {
        if (errors.Count == 0)
        {
            return [];
        }

        var summaries = new List<TraceExceptionSummary>(errors.Count);
        foreach (var error in errors)
        {
            summaries.Add(new TraceExceptionSummary
            {
                ExceptionType = error.ExceptionType,
                ExceptionMessage = error.ExceptionMessage,
                SpanId = error.SpanId.Value,
                Service = error.Service,
                Operation = error.Operation,
            });
        }

        return summaries;
    }
}
