namespace Tessera.Integration.Seed;

/// <summary>
///     OTLP/JSON maps protobuf <c>bytes</c> fields to base64. Pure helpers for
///     hex ↔ base64 used by <see cref="GoldenSeeder" /> payloads.
/// </summary>
public static class OtlpIdEncoding
{
    /// <summary>
    ///     Convert a lowercase/uppercase hex string (even length) to the base64
    ///     form expected by OTLP JSON for <c>traceId</c> / <c>spanId</c>.
    /// </summary>
    /// <exception cref="ArgumentException"></exception>
    public static string HexToBase64(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex) || hex.Length % 2 != 0)
        {
            throw new ArgumentException("hex id must be non-empty even-length", nameof(hex));
        }

        var bytes = Convert.FromHexString(hex);
        return Convert.ToBase64String(bytes);
    }

    /// <summary>Build absolute insert URL for VictoriaTraces OTLP HTTP.</summary>
    public static string TracesOtlpInsertUrl(string tracesBase)
    {
        return Combine(tracesBase, "/insert/opentelemetry/v1/traces");
    }

    /// <summary>Build absolute insert URL for VictoriaLogs OTLP HTTP.</summary>
    public static string LogsOtlpInsertUrl(string logsBase)
    {
        return Combine(logsBase, "/insert/opentelemetry/v1/logs");
    }

    /// <summary>Build absolute insert URL for VictoriaLogs JSON line API.</summary>
    public static string LogsJsonLineInsertUrl(string logsBase)
    {
        return Combine(logsBase, "/insert/jsonline");
    }

    private static string Combine(string baseUrl, string relative)
    {
        return baseUrl.TrimEnd('/') + relative;
    }
}
