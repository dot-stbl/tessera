namespace Tessera.Shared.Kernel.Providers.Traces;

using Tessera.Shared.Kernel.Domain.Traces;
using Tessera.Shared.Kernel.Identifiers;
using Tessera.Shared.Kernel.Pagination;

/// <summary>
/// Abstraction over a trace data source backend (VictoriaTraces, Tempo, Jaeger, etc.).
/// Modules depend on this interface; concrete providers live in
/// <c>Tessera.Providers.&lt;Name&gt;</c>.
/// </summary>
public interface ITraceProvider
{
    /// <summary>
    /// Search traces by query parameters. Returns a cursor-paginated page.
    /// </summary>
    Task<Page<TraceSummary>> SearchAsync(TraceSearchQuery query, CancellationToken ct);

    /// <summary>
    /// Fetch a full trace by ID, including the reconstructed span tree.
    /// Returns null when the trace is not found.
    /// </summary>
    Task<TraceDetail?> GetByIdAsync(TraceId traceId, CancellationToken ct);
}