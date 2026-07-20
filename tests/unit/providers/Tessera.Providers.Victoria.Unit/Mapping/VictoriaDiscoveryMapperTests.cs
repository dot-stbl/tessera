using Tessera.Providers.Victoria.Implementation.Mapping;
using Xunit;

namespace Tessera.Providers.Victoria.Unit.Mapping;

/// <summary>
///     Unit tests for <see cref="VictoriaDiscoveryMapper" /> — VL <c>_stream</c>
///     extraction, deduplication, and service summary composition.
/// </summary>
public sealed class VictoriaDiscoveryMapperTests
{
    private static readonly string[] ExpectedDistinctStreams = ["api", "web", "db"];
    private static readonly string[] ExpectedMergedServices = ["checkout-api", "postgres", "redis"];

    /// <summary>
    ///     NDJSON lines either contain the <c>_stream</c> field (we extract it)
    ///     or don't (we return null). Malformed lines yield null.
    /// </summary>
    [Theory]
    [InlineData(/*lang=json,strict*/ "{\"_stream\":\"checkout-api\"}", "checkout-api")]
    [InlineData(/*lang=json,strict*/ "{\"_stream\":\"api\", \"msg\":\"hello\"}", "api")]
    [InlineData(/*lang=json,strict*/ "{\"msg\":\"no_stream\"}", null)]
    [InlineData("garbage", null)]
    public void ExtractStreamField_ParsesNdjsonLines(string line, string? expected)
    {
        Assert.Equal(expected, VictoriaDiscoveryMapper.ExtractStreamField(line));
    }

    /// <summary>
    ///     Multiple NDJSON lines with duplicate streams collapse to one entry,
    ///     preserving first-occurrence order.
    /// </summary>
    [Fact]
    public void ExtractDistinctStreams_DeduplicatesInOrder()
    {
        var lines = new[]
        {
            /*lang=json,strict*/
                                 "{\"_stream\":\"api\"}",
            /*lang=json,strict*/
                                 "{\"_stream\":\"web\"}",
            /*lang=json,strict*/
                                 "{\"_stream\":\"api\"}",
            /*lang=json,strict*/
                                 "{\"_stream\":\"db\"}",
        };

        var streams = VictoriaDiscoveryMapper.ExtractDistinctStreams(string.Join('\n', lines));

        Assert.Equal(ExpectedDistinctStreams, streams);
    }

    /// <summary>
    ///     Service list merges trace services + log streams, dedupes,
    ///     sorts alphabetically, and yields one <see cref="Tessera.Shared.Kernel.Domain.Services.ServiceSummary" /> per entry.
    /// </summary>
    [Fact]
    public void ToServiceList_MergesAndDeduplicates()
    {
        var traceServices = new[] { "checkout-api", "postgres" };
        var logStreams = new[] { "checkout-api", "redis" };

        var services = VictoriaDiscoveryMapper.ToServiceList(traceServices, logStreams);

        Assert.Equal(3, services.Count);
        Assert.Equal(ExpectedMergedServices, services.Select(static s => s.Name).ToArray());
        Assert.All(services, static s => Assert.Equal(0, s.SpanCount));
    }
}
