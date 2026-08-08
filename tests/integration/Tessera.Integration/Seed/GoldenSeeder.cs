using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Tessera.Integration.Support;

namespace Tessera.Integration.Seed;

/// <summary>
///     Writes <see cref="GoldenSeed" /> identity into VictoriaTraces + VictoriaLogs
///     without an OTEL collector. Prefer OTLP/HTTP; VL falls back to jsonline.
/// </summary>
public sealed class GoldenSeeder(HttpClient http)
{
    /// <summary>
    ///     Seed a small ok-path span tree + ≥2 correlated log lines.
    ///     Re-run is idempotent enough for search-by-id assertions.
    /// </summary>
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var nowUnixNano = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1_000_000L;
        await GoldenSeederHttp.PostTracesAsync(http, nowUnixNano, cancellationToken);
        await GoldenSeederHttp.PostLogsAsync(http, nowUnixNano, cancellationToken);
    }

    /// <summary>OTLP JSON ExportTraceServiceRequest for the golden span tree.</summary>
    public static string BuildTracesOtlpJson(long startUnixNano)
    {
        return GoldenSeederPayloads.BuildTracesOtlpJson(startUnixNano);
    }

    /// <summary>OTLP JSON ExportLogsServiceRequest with two correlated lines.</summary>
    public static string BuildLogsOtlpJson(long startUnixNano)
    {
        return GoldenSeederPayloads.BuildLogsOtlpJson(startUnixNano);
    }

    /// <summary>
    ///     NDJSON body for VL <c>/insert/jsonline</c> with correlation fields
    ///     Tessera queries (<c>_trace_id</c> / <c>_span_id</c>).
    /// </summary>
    public static string BuildLogsJsonLine(long startUnixNano)
    {
        return GoldenSeederPayloads.BuildLogsJsonLine(startUnixNano);
    }
}

/// <summary>HTTP post paths for <see cref="GoldenSeeder" />.</summary>
file static class GoldenSeederHttp
{
    public static async Task PostTracesAsync(
        HttpClient http,
        long startUnixNano,
        CancellationToken cancellationToken)
    {
        var body = GoldenSeederPayloads.BuildTracesOtlpJson(startUnixNano);
        var url = OtlpIdEncoding.TracesOtlpInsertUrl(VictoriaEndpoints.TracesBase);
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = await http.PostAsync(new Uri(url), content, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"VT OTLP insert {(int)response.StatusCode} {url}: {text}");
        }
    }

    public static async Task PostLogsAsync(
        HttpClient http,
        long startUnixNano,
        CancellationToken cancellationToken)
    {
        var otlpBody = GoldenSeederPayloads.BuildLogsOtlpJson(startUnixNano);
        var otlpUrl = OtlpIdEncoding.LogsOtlpInsertUrl(VictoriaEndpoints.LogsBase);
        using (var content = new StringContent(otlpBody, Encoding.UTF8, "application/json"))
        using (var response = await http.PostAsync(new Uri(otlpUrl), content, cancellationToken))
        {
            if (response.IsSuccessStatusCode)
            {
                return;
            }
        }

        var jsonLine = GoldenSeederPayloads.BuildLogsJsonLine(startUnixNano);
        var jsonUrl = OtlpIdEncoding.LogsJsonLineInsertUrl(VictoriaEndpoints.LogsBase);
        using var lineContent = new StringContent(jsonLine, Encoding.UTF8);
        lineContent.Headers.ContentType = new MediaTypeHeaderValue("application/stream+json");
        using var lineResponse = await http.PostAsync(new Uri(jsonUrl), lineContent, cancellationToken);
        if (!lineResponse.IsSuccessStatusCode)
        {
            var text = await lineResponse.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"VL log insert failed OTLP+jsonline {(int)lineResponse.StatusCode} {jsonUrl}: {text}");
        }
    }
}

/// <summary>Pure JSON payload builders for golden seed inserts.</summary>
file static class GoldenSeederPayloads
{
    private static readonly JsonSerializerOptions JsonOptions = JsonSerializerOptions.Web;

    public static string BuildTracesOtlpJson(long startUnixNano)
    {
        var endUnixNano = startUnixNano + 25_000_000L;
        var childStart = startUnixNano + 1_000_000L;
        var childEnd = startUnixNano + 10_000_000L;
        var traceB64 = OtlpIdEncoding.HexToBase64(GoldenSeed.TraceId);
        var rootB64 = OtlpIdEncoding.HexToBase64(GoldenSeed.RootSpanId);
        var childB64 = OtlpIdEncoding.HexToBase64(GoldenSeed.ErrorSpanId);

        var payload = new
        {
            resourceSpans = new[]
            {
                new
                {
                    resource = new
                    {
                        attributes = new[]
                        {
                            StringAttr("service.name", GoldenSeed.ServiceName),
                        },
                    },
                    scopeSpans = new[]
                    {
                        new
                        {
                            scope = new { name = "tessera.integration", version = "1" },
                            spans = new object[]
                            {
                                new
                                {
                                    traceId = traceB64,
                                    spanId = rootB64,
                                    name = GoldenSeed.RootOperation,
                                    kind = 2,
                                    startTimeUnixNano = startUnixNano.ToString(CultureInfo.InvariantCulture),
                                    endTimeUnixNano = endUnixNano.ToString(CultureInfo.InvariantCulture),
                                    status = new { code = 1 },
                                },
                                new
                                {
                                    traceId = traceB64,
                                    spanId = childB64,
                                    parentSpanId = rootB64,
                                    name = "GET /it/payment",
                                    kind = 3,
                                    startTimeUnixNano = childStart.ToString(CultureInfo.InvariantCulture),
                                    endTimeUnixNano = childEnd.ToString(CultureInfo.InvariantCulture),
                                    status = new { code = 1 },
                                },
                            },
                        },
                    },
                },
            },
        };

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    public static string BuildLogsOtlpJson(long startUnixNano)
    {
        var traceB64 = OtlpIdEncoding.HexToBase64(GoldenSeed.TraceId);
        var rootB64 = OtlpIdEncoding.HexToBase64(GoldenSeed.RootSpanId);
        var second = startUnixNano + 2_000_000L;

        var payload = new
        {
            resourceLogs = new[]
            {
                new
                {
                    resource = new
                    {
                        attributes = new[]
                        {
                            StringAttr("service.name", GoldenSeed.ServiceName),
                        },
                    },
                    scopeLogs = new[]
                    {
                        new
                        {
                            scope = new { name = "tessera.integration", version = "1" },
                            logRecords = new object[]
                            {
                                new
                                {
                                    timeUnixNano = startUnixNano.ToString(CultureInfo.InvariantCulture),
                                    severityNumber = 9,
                                    severityText = "INFO",
                                    body = new { stringValue = "checkout started" },
                                    attributes = new[]
                                    {
                                        StringAttr("trace_id", GoldenSeed.TraceId),
                                        StringAttr("span_id", GoldenSeed.RootSpanId),
                                    },
                                    traceId = traceB64,
                                    spanId = rootB64,
                                },
                                new
                                {
                                    timeUnixNano = second.ToString(CultureInfo.InvariantCulture),
                                    severityNumber = 9,
                                    severityText = "INFO",
                                    body = new { stringValue = "checkout completed" },
                                    attributes = new[]
                                    {
                                        StringAttr("trace_id", GoldenSeed.TraceId),
                                        StringAttr("span_id", GoldenSeed.RootSpanId),
                                    },
                                    traceId = traceB64,
                                    spanId = rootB64,
                                },
                            },
                        },
                    },
                },
            },
        };

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    public static string BuildLogsJsonLine(long startUnixNano)
    {
        var t0 = DateTimeOffset.FromUnixTimeMilliseconds(startUnixNano / 1_000_000L);
        var t1 = t0.AddMilliseconds(2);
        var line1 = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["_time"] = t0.ToString("O"),
            ["_msg"] = "checkout started",
            ["level"] = "info",
            ["service"] = GoldenSeed.ServiceName,
            ["_trace_id"] = GoldenSeed.TraceId,
            ["_span_id"] = GoldenSeed.RootSpanId,
            ["trace_id"] = GoldenSeed.TraceId,
            ["span_id"] = GoldenSeed.RootSpanId,
        }, JsonOptions);
        var line2 = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["_time"] = t1.ToString("O"),
            ["_msg"] = "checkout completed",
            ["level"] = "info",
            ["service"] = GoldenSeed.ServiceName,
            ["_trace_id"] = GoldenSeed.TraceId,
            ["_span_id"] = GoldenSeed.RootSpanId,
            ["trace_id"] = GoldenSeed.TraceId,
            ["span_id"] = GoldenSeed.RootSpanId,
        }, JsonOptions);
        return line1 + "\n" + line2 + "\n";
    }

    private static object StringAttr(string key, string value)
    {
        return new { key, value = new { stringValue = value } };
    }
}
