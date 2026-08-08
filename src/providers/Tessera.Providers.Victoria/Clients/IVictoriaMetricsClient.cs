using Refit;
using Tessera.Providers.Victoria.Dto.Prometheus.Labels;
using Tessera.Providers.Victoria.Dto.Prometheus.Query;

namespace Tessera.Providers.Victoria.Clients;

/// <summary>
///     Refit client for the Prometheus-compatible API on VictoriaMetrics
///     single-node (vmsingle) / vmui. Paths are the standard Prometheus
///     surface (<c>/api/v1/...</c>), not the cluster select prefix used by
///     vtselect/vlselect.
/// </summary>
public interface IVictoriaMetricsClient
{
    /// <summary><c>GET /api/v1/query</c> — instant query.</summary>
    [Get("/api/v1/query")]
    public Task<PromQueryResponse> QueryAsync(
        [Query("query")] string query,
        [Query("time")] string? time,
        CancellationToken cancellationToken);

    /// <summary><c>GET /api/v1/query_range</c> — range query.</summary>
    [Get("/api/v1/query_range")]
    public Task<PromQueryResponse> QueryRangeAsync(
        [Query("query")] string query,
        [Query("start")] string start,
        [Query("end")] string end,
        [Query("step")] string step,
        CancellationToken cancellationToken);

    /// <summary><c>GET /api/v1/label/{name}/values</c> — distinct label values.</summary>
    [Get("/api/v1/label/{name}/values")]
    public Task<PromLabelValuesResponse> LabelValuesAsync(
        string name,
        [Query("start")] string? start,
        [Query("end")] string? end,
        CancellationToken cancellationToken);
}
