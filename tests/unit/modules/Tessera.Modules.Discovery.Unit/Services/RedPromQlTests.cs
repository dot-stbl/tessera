using Tessera.Modules.Discovery.Services.Red;

namespace Tessera.Modules.Discovery.Unit.Services;

/// <summary>
///     PromQL label escape and RED templates.
/// </summary>
public sealed class RedPromQlTests
{
    /// <inheritdoc/>
    [Theory]
    [InlineData("api", "api")]
    [InlineData("a\"b", "a\\\"b")]
    [InlineData("a\\b", "a\\\\b")]
    [InlineData("a\nb", "a\\nb")]
    public void EscapeLabelValue_EscapesSpecialCharacters(string input, string expected)
    {
        Assert.Equal(expected, RedPromQl.EscapeLabelValue(input));
    }

    /// <inheritdoc/>
    [Fact]
    public void RequestRate_WithoutOperation_UsesServiceLabelOnly()
    {
        var query = RedPromQl.RequestRate("checkout", operation: null);

        Assert.Equal(
            "sum(rate(http_server_request_duration_seconds_count{service_name=\"checkout\"}[5m]))",
            query);
    }

    /// <inheritdoc/>
    [Fact]
    public void RequestRate_WithOperation_AddsHttpRoute()
    {
        var query = RedPromQl.RequestRate("checkout", "GET /pay");

        Assert.Contains("service_name=\"checkout\"", query, StringComparison.Ordinal);
        Assert.Contains("http_route=\"GET /pay\"", query, StringComparison.Ordinal);
    }

    /// <inheritdoc/>
    [Fact]
    public void ErrorRate_Filters5xxStatusClass()
    {
        var query = RedPromQl.ErrorRate("api", null);

        Assert.Contains("http_response_status_code=~\"5..\"", query, StringComparison.Ordinal);
        Assert.Contains("service_name=\"api\"", query, StringComparison.Ordinal);
    }

    /// <inheritdoc/>
    [Fact]
    public void DurationP95_UsesHistogramQuantile()
    {
        var query = RedPromQl.DurationP95("api", null);

        Assert.StartsWith("histogram_quantile(0.95,", query, StringComparison.Ordinal);
        Assert.Contains("http_server_request_duration_seconds_bucket", query, StringComparison.Ordinal);
    }

    /// <inheritdoc/>
    [Fact]
    public void RequestRate_EscapesQuotesInServiceName()
    {
        var query = RedPromQl.RequestRate("svc\"x", null);

        Assert.Contains("service_name=\"svc\\\"x\"", query, StringComparison.Ordinal);
    }
}
