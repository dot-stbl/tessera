using Tessera.Modules.Traces.Contracts.Errors;
using Tessera.Modules.Traces.Services.Errors;
using Tessera.Shared.Kernel.Exceptions;
using Tessera.Shared.Kernel.Providers.Traces;

namespace Tessera.Modules.Traces.Services;

/// <summary>
///     Orchestrates the errors inbox: search recent traces, keep root-status
///     Error summaries, fetch a capped set of details, group span exceptions
///     by type + normalized message.
/// </summary>
public sealed class ErrorsInboxService(ITraceProvider traceProvider)
{
    /// <summary>
    ///     Hard cap on <see cref="ITraceProvider.GetByIdAsync" /> calls per
    ///     inbox request (bounds upstream cost). Documented for operators.
    /// </summary>
    public const int MaxDetailFetches = 20;

    /// <summary>Default search page size when the client omits limit.</summary>
    public const int DefaultSearchLimit = 50;

    /// <summary>Max sample trace ids retained per error group.</summary>
    public const int MaxSampleTraceIds = 5;

    /// <summary>
    ///     Build grouped error summaries for the given window. Requires a
    ///     non-empty time range (<paramref name="request" /> start/end).
    /// </summary>
    /// <exception cref="ProviderException">When start/end are missing or inverted.</exception>
    public async Task<IReadOnlyList<ErrorGroupSummary>> ListAsync(
        ListErrorsRequest request,
        CancellationToken cancellationToken = default)
    {
        ErrorsInboxValidation.EnsureValidRange(request);

        var searchLimit = request.Limit is > 0 ? request.Limit.Value : DefaultSearchLimit;
        var page = await traceProvider.SearchAsync(
            new TraceSearchQuery(
                request.Service,
                Operation: null,
                request.StartUnixMs,
                request.EndUnixMs,
                MinDurationMs: null,
                MaxDurationMs: null,
                Cursor: null,
                Limit: searchLimit),
            cancellationToken);

        var errorSummaries = ErrorsInboxGrouping.FilterErrorSummaries(page.Items);
        var details = await ErrorsInboxFetch.FetchDetailsAsync(
            traceProvider,
            errorSummaries,
            MaxDetailFetches,
            cancellationToken);

        return ErrorsInboxGrouping.GroupErrors(details, MaxSampleTraceIds);
    }
}
