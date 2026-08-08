namespace Tessera.Integration.Support;

/// <summary>
///     Default loopback endpoints for <c>stack/docker-compose.yml</c> port maps
///     (brought up with <c>podman compose</c>). Override via env if needed.
/// </summary>
public static class VictoriaEndpoints
{
    /// <summary>VictoriaTraces HTTP (Jaeger select base).</summary>
    public static string TracesBase { get; } =
        Environment.GetEnvironmentVariable("TESSERA_IT_TRACES_URL")
        ?? "http://127.0.0.1:10428";

    /// <summary>VictoriaLogs HTTP.</summary>
    public static string LogsBase { get; } =
        Environment.GetEnvironmentVariable("TESSERA_IT_LOGS_URL")
        ?? "http://127.0.0.1:9428";

    /// <summary>VictoriaMetrics HTTP (wave 2).</summary>
    public static string MetricsBase { get; } =
        Environment.GetEnvironmentVariable("TESSERA_IT_METRICS_URL")
        ?? "http://127.0.0.1:8428";
}
