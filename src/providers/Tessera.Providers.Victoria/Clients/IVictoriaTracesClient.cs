using Refit;
using Tessera.Providers.Victoria.Dto.Jaeger;
using Tessera.Providers.Victoria.Dto.Jaeger.Trace;

namespace Tessera.Providers.Victoria.Clients;

/// <summary>
///     Refit client for the Jaeger-compatible API on single-node VictoriaTraces.
///     Paths are <c>/select/jaeger/api/...</c> (no account/project tenant segment).
/// </summary>
public interface IVictoriaTracesClient
{
    /// <summary><c>GET /select/jaeger/api/services</c>.</summary>
    [Get("/select/jaeger/api/services")]
    public Task<JaegerResponse<string>> GetServicesAsync(CancellationToken cancellationToken);

    /// <summary><c>GET /select/jaeger/api/services/{service}/operations</c>.</summary>
    [Get("/select/jaeger/api/services/{service}/operations")]
    public Task<JaegerResponse<string>> GetOperationsAsync(
        string service,
        CancellationToken cancellationToken);

    /// <summary>
    ///     <c>GET /select/jaeger/api/traces</c> — search. Prefer
    ///     <paramref name="lookback" /> when start/end are awkward; VT accepts
    ///     Jaeger-style query params.
    /// </summary>
    [Get("/select/jaeger/api/traces")]
    public Task<JaegerResponse<JaegerTrace>> SearchTracesAsync(
        [Query] string? service,
        [Query] string? operation,
        [Query] string? tags,
        [Query] long? start,
        [Query] long? end,
        [Query] string? lookback,
        [Query] string? minDuration,
        [Query] string? maxDuration,
        [Query] int? limit,
        CancellationToken cancellationToken);

    /// <summary><c>GET /select/jaeger/api/traces/{traceId}</c>.</summary>
    [Get("/select/jaeger/api/traces/{traceId}")]
    public Task<JaegerResponse<JaegerTrace>> GetTraceAsync(
        string traceId,
        CancellationToken cancellationToken);
}
