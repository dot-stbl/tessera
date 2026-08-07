using System.Globalization;
using System.Text.Json;
using Tessera.Providers.Victoria.Dto.Prometheus.Labels;
using Tessera.Providers.Victoria.Dto.Prometheus.Query;
using Tessera.Shared.Kernel.Domain.Metrics.Results;
using Tessera.Shared.Kernel.Domain.Metrics.Samples;
using Tessera.Shared.Kernel.Domain.Metrics.Series;
using Tessera.Shared.Kernel.Exceptions;

namespace Tessera.Providers.Victoria.Implementation.Metrics;

/// <summary>
///     Pure Prom JSON → domain metrics mapping. Times are unix seconds on the
///     wire and converted to unix milliseconds for the domain.
/// </summary>
internal static class VictoriaMetricsMapper
{
    /// <summary>
    ///     Map a range-query response to <see cref="MetricMatrix" />.
    /// </summary>
    /// <exception cref="ProviderException"></exception>
    public static MetricMatrix ToMatrix(PromQueryResponse response)
    {
        VictoriaMetricsMappingHelpers.EnsureOk(response);
        var data = response.Data
            ?? throw new ProviderException("provider.mapping_error", "metrics response missing data");

        if (!string.Equals(data.ResultType, "matrix", StringComparison.Ordinal))
        {
            throw new ProviderException(
                "provider.mapping_error",
                $"expected matrix resultType, got '{data.ResultType}'");
        }

        var rows = VictoriaMetricsMappingHelpers.DeserializeSeriesArray(data.Result);
        var series = new List<MetricSeries>(rows.Count);
        for (var i = 0; i < rows.Count; i++)
        {
            series.Add(VictoriaMetricsMappingHelpers.ToSeriesFromMatrixRow(rows[i]));
        }

        return new MetricMatrix(series);
    }

    /// <summary>
    ///     Map an instant-query response to <see cref="MetricVector" />.
    ///     Scalar results are wrapped as a single unlabelled series.
    /// </summary>
    /// <exception cref="ProviderException"></exception>
    public static MetricVector ToVector(PromQueryResponse response)
    {
        VictoriaMetricsMappingHelpers.EnsureOk(response);
        var data = response.Data
            ?? throw new ProviderException("provider.mapping_error", "metrics response missing data");

        if (string.Equals(data.ResultType, "scalar", StringComparison.Ordinal))
        {
            var sample = VictoriaMetricsMappingHelpers.ParseSamplePair(data.Result);
            return new MetricVector(
            [
                new MetricSeries(
                    new Dictionary<string, string>(StringComparer.Ordinal),
                    [sample]),
            ]);
        }

        if (!string.Equals(data.ResultType, "vector", StringComparison.Ordinal))
        {
            throw new ProviderException(
                "provider.mapping_error",
                $"expected vector or scalar resultType, got '{data.ResultType}'");
        }

        var rows = VictoriaMetricsMappingHelpers.DeserializeSeriesArray(data.Result);
        var series = new List<MetricSeries>(rows.Count);
        for (var i = 0; i < rows.Count; i++)
        {
            series.Add(VictoriaMetricsMappingHelpers.ToSeriesFromVectorRow(rows[i]));
        }

        return new MetricVector(series);
    }

    /// <summary>
    ///     Map label-values response to a string list.
    /// </summary>
    /// <exception cref="ProviderException"></exception>
    public static IReadOnlyList<string> ToLabelValues(PromLabelValuesResponse response)
    {
        if (!string.Equals(response.Status, "success", StringComparison.Ordinal))
        {
            throw new ProviderException(
                "provider.network_error",
                response.Error ?? "label values query failed");
        }

        return response.Data ?? [];
    }

    /// <summary>
    ///     Convert Prom unix-seconds (double) to domain unix milliseconds.
    /// </summary>
    public static long SecondsToUnixMs(double unixSeconds)
    {
        return (long)(unixSeconds * 1000d);
    }

    /// <summary>
    ///     Parse a string-encoded Prom sample value.
    /// </summary>
    public static double ParseSampleValue(string raw)
    {
        return double.Parse(raw, NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    /// <summary>
    ///     Format a unix-ms instant as Prom query time (seconds, invariant).
    /// </summary>
    public static string FormatUnixSeconds(long unixMs)
    {
        return (unixMs / 1000d).ToString(CultureInfo.InvariantCulture);
    }
}

/// <summary>
///     File-local helpers for Prom series / sample parsing (no private methods
///     on <see cref="VictoriaMetricsMapper" />).
/// </summary>
file static class VictoriaMetricsMappingHelpers
{
    public static void EnsureOk(PromQueryResponse response)
    {
        if (!string.Equals(response.Status, "success", StringComparison.Ordinal))
        {
            throw new ProviderException(
                "provider.network_error",
                response.Error ?? "metrics query failed");
        }
    }

    public static List<PromSeriesResult> DeserializeSeriesArray(JsonElement result)
    {
        if (result.ValueKind != JsonValueKind.Array)
        {
            throw new ProviderException("provider.mapping_error", "metrics result is not an array");
        }

        return JsonSerializer.Deserialize<List<PromSeriesResult>>(result.GetRawText()) ?? [];
    }

    public static MetricSeries ToSeriesFromMatrixRow(PromSeriesResult row)
    {
        var labels = CopyLabels(row.Metric);

        var samples = new List<MetricSample>();
        if (row.Values is not null)
        {
            for (var i = 0; i < row.Values.Count; i++)
            {
                samples.Add(ParseSamplePair(row.Values[i]));
            }
        }

        return new MetricSeries(labels, samples);
    }

    public static MetricSeries ToSeriesFromVectorRow(PromSeriesResult row)
    {
        var labels = CopyLabels(row.Metric);

        if (row.Value is null || row.Value.Value.ValueKind == JsonValueKind.Undefined)
        {
            return new MetricSeries(labels, []);
        }

        var sample = ParseSamplePair(row.Value.Value);
        return new MetricSeries(labels, [sample]);
    }

    public static IReadOnlyDictionary<string, string> CopyLabels(
        IReadOnlyDictionary<string, string>? metric)
    {
        return metric is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(metric, StringComparer.Ordinal);
    }

    public static MetricSample ParseSamplePair(JsonElement pair)
    {
        if (pair.ValueKind != JsonValueKind.Array || pair.GetArrayLength() < 2)
        {
            throw new ProviderException("provider.mapping_error", "invalid sample pair");
        }

        var timeElement = pair[0];
        var valueElement = pair[1];
        var unixSeconds = timeElement.ValueKind == JsonValueKind.Number
            ? timeElement.GetDouble()
            : double.Parse(timeElement.GetString()!, CultureInfo.InvariantCulture);
        var valueText = valueElement.ValueKind == JsonValueKind.String
            ? valueElement.GetString()!
            : valueElement.GetRawText();

        return new MetricSample(
            VictoriaMetricsMapper.SecondsToUnixMs(unixSeconds),
            VictoriaMetricsMapper.ParseSampleValue(valueText));
    }
}
