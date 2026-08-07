using System.Text.Json;
using Tessera.Providers.Victoria.Dto.Prometheus.Labels;
using Tessera.Providers.Victoria.Dto.Prometheus.Query;
using Tessera.Providers.Victoria.Implementation.Metrics;
using Tessera.Shared.Kernel.Exceptions;
using Xunit;

namespace Tessera.Providers.Victoria.Unit.Mapping;

/// <summary>
///     Unit tests for Prom JSON → domain mapping in <see cref="VictoriaMetricsMapper" />.
/// </summary>
public sealed class VictoriaMetricsMapperTests
{
    /// <summary>
    ///     Matrix result maps series labels and samples with seconds→ms.
    /// </summary>
    [Fact]
    public void ToMatrix_MapsSeriesAndSamples()
    {
        const string resultJson = """
            [
              {
                "metric": { "__name__": "up", "job": "api" },
                "values": [[1700000000, "1"], [1700000060, "0"]]
              }
            ]
            """;
        var response = new PromQueryResponse(
            Status: "success",
            Data: new PromQueryData("matrix", JsonDocument.Parse(resultJson).RootElement.Clone()),
            ErrorType: null,
            Error: null);

        var matrix = VictoriaMetricsMapper.ToMatrix(response);

        Assert.Single(matrix.Series);
        Assert.Equal("up", matrix.Series[0].Labels["__name__"]);
        Assert.Equal(2, matrix.Series[0].Samples.Count);
        Assert.Equal(1_700_000_000_000, matrix.Series[0].Samples[0].TimestampUnixMs);
        Assert.Equal(1d, matrix.Series[0].Samples[0].Value);
        Assert.Equal(0d, matrix.Series[0].Samples[1].Value);
    }

    /// <summary>
    ///     Vector result maps a single sample per series.
    /// </summary>
    [Fact]
    public void ToVector_MapsSingleSample()
    {
        const string resultJson = """
            [
              {
                "metric": { "service": "checkout" },
                "value": [1700000000.5, "42.5"]
              }
            ]
            """;
        var response = new PromQueryResponse(
            Status: "success",
            Data: new PromQueryData("vector", JsonDocument.Parse(resultJson).RootElement.Clone()),
            ErrorType: null,
            Error: null);

        var vector = VictoriaMetricsMapper.ToVector(response);

        Assert.Single(vector.Series);
        Assert.Equal("checkout", vector.Series[0].Labels["service"]);
        Assert.Equal(1_700_000_000_500, vector.Series[0].Samples[0].TimestampUnixMs);
        Assert.Equal(42.5d, vector.Series[0].Samples[0].Value);
    }

    /// <summary>
    ///     Scalar result is wrapped as one unlabelled series.
    /// </summary>
    [Fact]
    public void ToVector_Scalar_WrapsAsSeries()
    {
        const string resultJson = """[1700000000, "3"]""";
        var response = new PromQueryResponse(
            Status: "success",
            Data: new PromQueryData("scalar", JsonDocument.Parse(resultJson).RootElement.Clone()),
            ErrorType: null,
            Error: null);

        var vector = VictoriaMetricsMapper.ToVector(response);

        Assert.Single(vector.Series);
        Assert.Empty(vector.Series[0].Labels);
        Assert.Equal(3d, vector.Series[0].Samples[0].Value);
    }

    /// <summary>
    ///     Non-success status throws <see cref="ProviderException" />.
    /// </summary>
    [Fact]
    public void ToMatrix_ErrorStatus_Throws()
    {
        var response = new PromQueryResponse(
            Status: "error",
            Data: null,
            ErrorType: "bad_data",
            Error: "parse error");

        var exception = Assert.Throws<ProviderException>(() => VictoriaMetricsMapper.ToMatrix(response));
        Assert.Equal("provider.network_error", exception.Code);
    }

    /// <summary>
    ///     Label values response returns the data array.
    /// </summary>
    [Fact]
    public void ToLabelValues_ReturnsData()
    {
        var response = new PromLabelValuesResponse(
            Status: "success",
            Data: ["a", "b"],
            ErrorType: null,
            Error: null);

        Assert.Equal(["a", "b"], VictoriaMetricsMapper.ToLabelValues(response));
    }

    /// <summary>
    ///     Seconds→ms conversion multiplies by 1000.
    /// </summary>
    [Fact]
    public void SecondsToUnixMs_Multiplies()
    {
        Assert.Equal(1_700_000_000_000, VictoriaMetricsMapper.SecondsToUnixMs(1_700_000_000d));
    }

    /// <summary>
    ///     FormatUnixSeconds divides ms by 1000 for the Prom query string.
    /// </summary>
    [Fact]
    public void FormatUnixSeconds_Divides()
    {
        Assert.Equal("1700000000", VictoriaMetricsMapper.FormatUnixSeconds(1_700_000_000_000));
    }
}
