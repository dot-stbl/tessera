using Tessera.Shared.Kernel.Analysis.Red;
using Tessera.Shared.Kernel.Domain.Metrics.Results;
using Tessera.Shared.Kernel.Domain.Metrics.Samples;
using Tessera.Shared.Kernel.Domain.Metrics.Series;
using Xunit;

namespace Tessera.Shared.Unit.Analysis;

/// <summary>
///     Pure extract / ratio helpers for RED assembly.
/// </summary>
public sealed class RedMetricExtractTests
{
    private static readonly IReadOnlyDictionary<string, string> EmptyLabels =
        new Dictionary<string, string>();

    private static MetricSeries Series(params MetricSample[] samples)
    {
        return new MetricSeries(EmptyLabels, samples);
    }

    /// <inheritdoc/>
    [Fact]
    public void FirstSampleValue_Vector_ReturnsFirstSeriesFirstSample()
    {
        var vector = new MetricVector(
        [
            Series(new MetricSample(1_000L, 1.5), new MetricSample(2_000L, 9.0)),
            Series(new MetricSample(1_000L, 99.0)),
        ]);

        Assert.Equal(1.5, RedMetricExtract.FirstSampleValue(vector));
    }

    /// <inheritdoc/>
    [Fact]
    public void FirstSampleValue_EmptyVector_ReturnsNull()
    {
        Assert.Null(RedMetricExtract.FirstSampleValue(new MetricVector([])));
    }

    /// <inheritdoc/>
    [Fact]
    public void FirstSampleValue_Matrix_ReturnsFirstSeriesFirstSample()
    {
        var matrix = new MetricMatrix(
        [
            Series(new MetricSample(1_000L, 0.25), new MetricSample(2_000L, 0.5)),
        ]);

        Assert.Equal(0.25, RedMetricExtract.FirstSampleValue(matrix));
    }

    /// <inheritdoc/>
    [Fact]
    public void LastSampleValue_Matrix_ReturnsLastPoint()
    {
        var matrix = new MetricMatrix(
        [
            Series(new MetricSample(1_000L, 0.1), new MetricSample(2_000L, 0.9)),
        ]);

        Assert.Equal(0.9, RedMetricExtract.LastSampleValue(matrix));
    }

    /// <inheritdoc/>
    [Fact]
    public void FirstSampleValue_NaN_ReturnsNull()
    {
        var vector = new MetricVector([Series(new MetricSample(1L, double.NaN))]);

        Assert.Null(RedMetricExtract.FirstSampleValue(vector));
    }

    /// <inheritdoc/>
    [Theory]
    [InlineData(1.0, 4.0, 0.25)]
    [InlineData(0.0, 10.0, 0.0)]
    public void SafeRatio_ValidInputs_ReturnsQuotient(double top, double bottom, double expected)
    {
        Assert.Equal(expected, RedMetricExtract.SafeRatio(top, bottom));
    }

    /// <inheritdoc/>
    [Theory]
    [InlineData(1.0, 0.0)]
    [InlineData(1.0, double.NaN)]
    [InlineData(double.PositiveInfinity, 2.0)]
    public void SafeRatio_InvalidInputs_ReturnsNull(double top, double bottom)
    {
        Assert.Null(RedMetricExtract.SafeRatio(top, bottom));
    }

    /// <inheritdoc/>
    [Fact]
    public void SafeRatio_NullOperand_ReturnsNull()
    {
        Assert.Null(RedMetricExtract.SafeRatio(null, 1.0));
        Assert.Null(RedMetricExtract.SafeRatio(1.0, null));
    }

    /// <inheritdoc/>
    [Fact]
    public void SecondsToMilliseconds_ScalesByThousand()
    {
        Assert.Equal(1500d, RedMetricExtract.SecondsToMilliseconds(1.5));
        Assert.Null(RedMetricExtract.SecondsToMilliseconds(null));
        Assert.Null(RedMetricExtract.SecondsToMilliseconds(double.NaN));
    }
}
