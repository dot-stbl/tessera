using Tessera.Modules.Traces.Endpoints;
using Tessera.Modules.Traces.Errors;
using Tessera.Shared.Kernel.Analysis;
using Tessera.Shared.Kernel.Analysis.Assembly;
using Tessera.Shared.Kernel.Exceptions;
using Tessera.Shared.Kernel.Identifiers;
using Tessera.Shared.Kernel.Providers.Logs;
using Tessera.Shared.Kernel.Providers.Traces;
using Tessera.Shared.Kernel.Time;

namespace Tessera.Modules.Traces.Services;

/// <summary>
///     Orchestrates degradable request-view fetch: optional span tree +
///     correlated logs by <see cref="TraceId" />. Never 404 when either
///     source has data (ADR-0002 §2).
/// </summary>
public sealed class RequestViewService(
    ITraceProvider traceProvider,
    ILogProvider logProvider,
    TimeProvider clock)
{
    /// <summary>
    ///     Default lookback when the trace is missing so log-only views still
    ///     query a finite window (ListByTrace requires a range).
    /// </summary>
    private static readonly TimeSpan LogsOnlyDefaultWindow = TimeSpan.FromHours(24);

    /// <summary>
    ///     Load request view for <paramref name="traceId" />. Throws
    ///     <see cref="ProviderNotFoundException" /> only when both sources
    ///     are empty (<see cref="RequestViewMode.Empty" />).
    /// </summary>
    /// <exception cref="ProviderNotFoundException"></exception>
    public async Task<RequestView> GetAsync(TraceId traceId, CancellationToken cancellationToken = default)
    {
        var trace = await traceProvider.GetByIdAsync(traceId, cancellationToken);
        var range = trace is not null
            ? TracesEndpointHelpers.ToLogCorrelationRange(trace)
            : TimeRange.Last(LogsOnlyDefaultWindow, clock);
        var logs = await logProvider.ListByTraceAsync(traceId, range, cancellationToken);
        var view = RequestViewAnalysis.Build(trace, logs);

        if (view.Mode is RequestViewMode.Empty)
        {
            throw new ProviderNotFoundException(
                TracesErrors.TraceNotFound,
                $"trace {traceId.Value} not found");
        }

        return view;
    }
}
