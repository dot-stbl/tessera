namespace Tessera.Shared.Kernel.Time;

/// <summary>
///     Time range in unix milliseconds (inclusive on both ends). Used for trace/log
///     query windows.
/// </summary>
/// <param name="StartUnixMs">Start of the range, inclusive.</param>
/// <param name="EndUnixMs">End of the range, inclusive.</param>
public sealed record TimeRange(long StartUnixMs, long EndUnixMs)
{
    /// <summary>
    ///     Returns true when <paramref name="unixMs" /> falls within the range.
    /// </summary>
    public bool Contains(long unixMs)
    {
        return unixMs >= StartUnixMs && unixMs <= EndUnixMs;
    }

    /// <summary>
    ///     Intersection of two ranges. Returns null if they do not overlap.
    /// </summary>
    public TimeRange? Intersect(TimeRange other)
    {
        var start = Math.Max(StartUnixMs, other.StartUnixMs);
        var end = Math.Min(EndUnixMs, other.EndUnixMs);
        return start <= end ? new TimeRange(start, end) : null;
    }

    /// <summary>
    ///     Time range covering the last <paramref name="span" /> ending at the current UTC instant.
    ///     Uses <see cref="TimeProvider.System" /> — production code that
    ///     needs deterministic clock should call the <see cref="Last(TimeSpan, TimeProvider)" />
    ///     overload with an injected clock.
    /// </summary>
    public static TimeRange Last(TimeSpan span)
    {
        return Last(span, TimeProvider.System);
    }

    /// <summary>
    ///     Time range covering the last <paramref name="span" /> ending at the
    ///     supplied <paramref name="clock" /> instant. Injected-time version for
    ///     deterministic unit tests; production callers use
    ///     <see cref="Last(TimeSpan)" />.
    /// </summary>
    public static TimeRange Last(TimeSpan span, TimeProvider clock)
    {
        var end = clock.GetUtcNow().ToUnixTimeMilliseconds();
        var start = end - (long)span.TotalMilliseconds;
        return new TimeRange(start, end);
    }
}
