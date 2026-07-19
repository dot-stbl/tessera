using Refit;
using Tessera.Providers.Victoria.Dto.Jaeger;
using Tessera.Providers.Victoria.Dto.Jaeger.Trace;

namespace Tessera.Providers.Victoria.Clients;

/// <summary>
///     Refit client for the Jaeger-compatible API exposed by VictoriaTraces.
///     All endpoints are single-tenant (<c>{tenant}</c> path param, MVP value <c>"0"</c>).
/// </summary>
public interface IVictoriaTracesClient
{
    /// <summary>
    ///     <c>GET /select/{tenant}/jaeger/api/services</c> — list all services that have
    ///     produced traces.
    /// </summary>
    [Get("/select/{tenant}/jaeger/api/services")]
    public Task<JaegerResponse<string>> GetServicesAsync(
        string tenant,
        CancellationToken ct);

    /// <summary>
    ///     <c>GET /select/{tenant}/jaeger/api/services/{service}/operations</c> —
    ///     list all operations for a given service.
    /// </summary>
    [Get("/select/{tenant}/jaeger/api/services/{service}/operations")]
    public Task<JaegerResponse<string>> GetOperationsAsync(
        string tenant,
        string service,
        CancellationToken ct);

    /// <summary>
    ///     <c>GET /select/{tenant}/jaeger/api/traces</c> — search traces by filters.
    ///     Response is the Jaeger envelope; first span per trace is included
    ///     (use <see cref="GetTraceAsync" /> for the full span tree).
    /// </summary>
    [Get("/select/{tenant}/jaeger/api/traces")]
    public Task<JaegerResponse<JaegerTrace>> SearchTracesAsync(
        string tenant,
        [Query] string? service,
        [Query] string? operation,
        [Query] string? tags,
        [Query] long? start,
        [Query] long? end,
        [Query] string? minDuration,
        [Query] string? maxDuration,
        [Query] int? limit,
        CancellationToken ct);

    /// <summary>
    ///     <c>GET /select/{tenant}/jaeger/api/traces/{traceId}</c> — full trace by ID
    ///     (complete span set + processes).
    /// </summary>
    [Get("/select/{tenant}/jaeger/api/traces/{traceId}")]
    public Task<JaegerResponse<JaegerTrace>> GetTraceAsync(
        string tenant,
        string traceId,
        CancellationToken ct);
}
