using Tessera.Shared.Kernel.Time;
using Xunit;

namespace Tessera.Shared.Kernel.Tests.Time;

/// <summary>
///     Unit tests for <see cref="TimeRange" />.
/// </summary>
public sealed class TimeRangeTests
{
    /// <summary>
    ///     <see cref="TimeRange.Contains" /> returns true for unix-ms values strictly inside the range.
    /// </summary>
    [Fact]
    public void Contains_WithinRange_ReturnsTrue()
    {
        var range = new TimeRange(100, 200);

        Assert.True(range.Contains(150));
    }

    /// <summary>
    ///     <see cref="TimeRange.Contains" /> is inclusive on both endpoints — values equal to
    ///     <see cref="TimeRange.StartUnixMs" /> or <see cref="TimeRange.EndUnixMs" /> are contained.
    /// </summary>
    [Fact]
    public void Contains_AtBoundaries_ReturnsTrue()
    {
        var range = new TimeRange(100, 200);

        Assert.True(range.Contains(100));
        Assert.True(range.Contains(200));
    }

    /// <summary>
    ///     <see cref="TimeRange.Contains" /> returns false for unix-ms values outside the range.
    /// </summary>
    [Fact]
    public void Contains_OutsideRange_ReturnsFalse()
    {
        var range = new TimeRange(100, 200);

        Assert.False(range.Contains(99));
        Assert.False(range.Contains(201));
    }

    /// <summary>
    ///     <see cref="TimeRange.Intersect" /> of two overlapping ranges returns their intersection.
    /// </summary>
    [Fact]
    public void Intersect_OverlappingRanges_ReturnsIntersection()
    {
        var a = new TimeRange(100, 200);
        var b = new TimeRange(150, 250);

        var intersection = a.Intersect(b);

        Assert.NotNull(intersection);
        Assert.Equal(150, intersection!.StartUnixMs);
        Assert.Equal(200, intersection.EndUnixMs);
    }

    /// <summary>
    ///     <see cref="TimeRange.Intersect" /> of non-overlapping ranges returns null.
    /// </summary>
    [Fact]
    public void Intersect_NonOverlappingRanges_ReturnsNull()
    {
        var a = new TimeRange(100, 200);
        var b = new TimeRange(300, 400);

        Assert.Null(a.Intersect(b));
    }

    /// <summary>
    ///     <see cref="TimeRange.Last" /> returns a range whose duration equals the supplied
    ///     <see cref="TimeSpan" /> and whose end aligns with the current UTC instant.
    /// </summary>
    [Fact]
    public void Last_ReturnsRangeWithExpectedDurationEndingNow()
    {
        var span = TimeSpan.FromMinutes(10);
        var beforeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var range = TimeRange.Last(span);

        var afterMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Assert.Equal((long)span.TotalMilliseconds, range.EndUnixMs - range.StartUnixMs);
        // end is between beforeMs (now) and afterMs (just-after-now)
        Assert.InRange(range.EndUnixMs, beforeMs, afterMs + 100);
        // start is end - span, so it's in the past
        Assert.True(range.StartUnixMs < range.EndUnixMs);
    }
}
