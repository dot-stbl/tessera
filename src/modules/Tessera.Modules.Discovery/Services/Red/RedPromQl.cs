using System.Text;

namespace Tessera.Modules.Discovery.Services.Red;

/// <summary>
///     PromQL builders for OTel HTTP server RED metrics. Label escape is
///     Prometheus-compatible (backslash + double-quote). Metric names follow
///     OTel HTTP semantic conventions (<c>http_server_request_duration_seconds_*</c>).
/// </summary>
internal static class RedPromQl
{
    /// <summary>Lookback window embedded in <c>rate(...[5m])</c>.</summary>
    public const string RateWindow = "5m";

    /// <summary>
    ///     Escape a PromQL double-quoted label value: <c>\</c> → <c>\\</c>,
    ///     <c>"</c> → <c>\"</c>, newline → <c>\n</c>.
    /// </summary>
    public static string EscapeLabelValue(string value)
    {
        var builder = new StringBuilder(value.Length + 8);
        foreach (var character in value)
        {
            switch (character)
            {
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                default:
                    builder.Append(character);
                    break;
            }
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Request rate (R): sum of rate of HTTP server request duration count.
    /// </summary>
    public static string RequestRate(string serviceName, string? operation)
    {
        return "sum(rate(http_server_request_duration_seconds_count{"
            + Selector(serviceName, operation)
            + "}["
            + RateWindow
            + "]))";
    }

    /// <summary>
    ///     Error rate numerator (5xx): same counter filtered by status class 5xx.
    /// </summary>
    public static string ErrorRate(string serviceName, string? operation)
    {
        return "sum(rate(http_server_request_duration_seconds_count{"
            + Selector(serviceName, operation)
            + ",http_response_status_code=~\"5..\"}["
            + RateWindow
            + "]))";
    }

    /// <summary>
    ///     Duration p95 (D): histogram quantile over the duration bucket series.
    /// </summary>
    public static string DurationP95(string serviceName, string? operation)
    {
        return "histogram_quantile(0.95, sum by (le) (rate(http_server_request_duration_seconds_bucket{"
            + Selector(serviceName, operation)
            + "}["
            + RateWindow
            + "])))";
    }

    /// <summary>
    ///     Label selector body without braces: <c>service_name="…"</c> and
    ///     optional <c>http_route="…"</c>.
    /// </summary>
    public static string Selector(string serviceName, string? operation)
    {
        var escapedService = EscapeLabelValue(serviceName);
        if (string.IsNullOrEmpty(operation))
        {
            return "service_name=\"" + escapedService + "\"";
        }

        var escapedOperation = EscapeLabelValue(operation);
        return "service_name=\"" + escapedService + "\",http_route=\"" + escapedOperation + "\"";
    }
}
