using Refit;
using Tessera.Providers.Victoria.Dto.Prometheus.Labels;
using Tessera.Providers.Victoria.Dto.Prometheus.Query;

namespace Tessera.Providers.Victoria.Clients;

/// <summary>
///     Refit client for the Prometheus-compatible API exposed by VictoriaMetrics.
///     Paths are tenant-prefixed like traces/logs (<c>/select/{tenant}/prometheus/...</c>).
/// </summary>
public interface IVictoriaMetricsClient
{
    /// <summary>
    ///     <c>GET /select/{tenant}/prometheus/api/v1/query</c> — instant query.
    /// </summary>
    [Get("/select/{tenant}/prometheus/api/v1/query")]
    public Task<PromQueryResponse> QueryAsync(
        string tenant,
        [Query("query")] string query,
        [Query("time")] string? time,
        CancellationToken cancellationToken);

    /// <summary>
    ///     <c>GET /select/{tenant}/prometheus/api/v1/query_range</c> — range query.
    /// </summary>
    [Get("/select/{tenant}/prometheus/api/v1/query_range")]
    public Task<PromQueryResponse> QueryRangeAsync(
        string tenant,
        [Query("query")] string query,
        [Query("start")] string start,
        [Query("end")] string end,
        [Query("step")] string step,
        CancellationToken cancellationToken);

    /// <summary>
    ///     <c>GET /select/{tenant}/prometheus/api/v1/label/{name}/values</c> —
    ///     distinct values for a label.
    /// </summary>
    [Get("/select/{tenant}/prometheus/api/v1/label/{name}/values")]
    public Task<PromLabelValuesResponse> LabelValuesAsync(
        string tenant,
        string name,
        [Query("start")] string? start,
        [Query("end")] string? end,
        CancellationToken cancellationToken);
}
