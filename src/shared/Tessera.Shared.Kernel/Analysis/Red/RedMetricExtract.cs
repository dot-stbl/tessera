using Tessera.Shared.Kernel.Domain.Metrics.Results;
using Tessera.Shared.Kernel.Domain.Metrics.Samples;
using Tessera.Shared.Kernel.Domain.Metrics.Series;

namespace Tessera.Shared.Kernel.Analysis.Red;

/// <summary>
///     Pure helpers for RED assembly: first-sample extract from PromQL results
///     and safe ratio math. No I/O.
/// </summary>
public static class RedMetricExtract
{
    /// <summary>
    ///     First series, first sample value from a vector result, or null when
    ///     empty / missing samples.
    /// </summary>
    public static double? FirstSampleValue(MetricVector vector)
    {
        return FirstSeriesSample(vector.Series, takeLast: false);
    }

    /// <summary>
    ///     First series, first sample value from a matrix result, or null when
    ///     empty / missing samples.
    /// </summary>
    public static double? FirstSampleValue(MetricMatrix matrix)
    {
        return FirstSeriesSample(matrix.Series, takeLast: false);
    }

    /// <summary>
    ///     Last sample of the first series (range query "current" point), or null.
    /// </summary>
    public static double? LastSampleValue(MetricMatrix matrix)
    {
        return FirstSeriesSample(matrix.Series, takeLast: true);
    }

    /// <summary>
    ///     <paramref name="numerator" /> / <paramref name="denominator" /> when
    ///     denominator is finite and non-zero; otherwise null.
    /// </summary>
    public static double? SafeRatio(double? numerator, double? denominator)
    {
        if (numerator is not { } top || denominator is not { } bottom)
        {
            return null;
        }

        if (double.IsNaN(bottom) || double.IsInfinity(bottom) || bottom == 0d)
        {
            return null;
        }

        if (double.IsNaN(top) || double.IsInfinity(top))
        {
            return null;
        }

        return top / bottom;
    }

    /// <summary>
    ///     Convert a duration expressed in seconds (OTel histogram unit) to
    ///     milliseconds for the RED wire shape.
    /// </summary>
    public static double? SecondsToMilliseconds(double? seconds)
    {
        if (seconds is not { } value)
        {
            return null;
        }

        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return null;
        }

        return value * 1000d;
    }

    /// <summary>
    ///     Read a sample value from the first series in <paramref name="series" />.
    ///     When <paramref name="takeLast" /> is true, uses the last sample; else the first.
    /// </summary>
    public static double? FirstSeriesSample(IReadOnlyList<MetricSeries> series, bool takeLast)
    {
        if (series is not { Count: > 0 })
        {
            return null;
        }

        return SeriesSample(series[0], takeLast);
    }

    /// <summary>
    ///     Read first or last sample value from a series.
    /// </summary>
    public static double? SeriesSample(MetricSeries series, bool takeLast)
    {
        if (series.Samples is not { Count: > 0 })
        {
            return null;
        }

        var sample = takeLast ? series.Samples[^1] : series.Samples[0];
        return FiniteSampleValue(sample);
    }

    /// <summary>
    ///     Return the sample value when finite; otherwise null.
    /// </summary>
    public static double? FiniteSampleValue(MetricSample sample)
    {
        if (double.IsNaN(sample.Value) || double.IsInfinity(sample.Value))
        {
            return null;
        }

        return sample.Value;
    }
}
