using Tessera.Shared.Kernel.Domain.Traces;
using Tessera.Shared.Kernel.Providers.Traces;

namespace Tessera.Modules.Traces.Services.Errors;

/// <summary>
///     Bounded detail-fetch for the errors inbox (sequential to avoid
///     stampeding the upstream).
/// </summary>
internal static class ErrorsInboxFetch
{
    /// <summary>
    ///     Fetch full traces for up to <paramref name="maxFetches" /> error
    ///     summaries, in list order. Skips nulls from the provider.
    /// </summary>
    public static async Task<IReadOnlyList<TraceDetail>> FetchDetailsAsync(
        ITraceProvider traceProvider,
        IReadOnlyList<TraceSummary> errorSummaries,
        int maxFetches,
        CancellationToken cancellationToken)
    {
        if (errorSummaries.Count == 0 || maxFetches <= 0)
        {
            return [];
        }

        var take = Math.Min(errorSummaries.Count, maxFetches);
        var details = new List<TraceDetail>(take);
        for (var index = 0; index < take; index++)
        {
            var detail = await traceProvider.GetByIdAsync(
                errorSummaries[index].TraceId,
                cancellationToken);
            if (detail is not null)
            {
                details.Add(detail);
            }
        }

        return details;
    }
}
