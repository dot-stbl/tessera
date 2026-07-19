using Tessera.Providers.Victoria.Implementation.Mapping;
using Tessera.Shared.Kernel.Identifiers;
using Tessera.Shared.Kernel.Providers.Logs;
using Tessera.Shared.Kernel.Time;
using Xunit;

namespace Tessera.Providers.Victoria.Unit.Mapping;

/// <summary>
///     Unit tests for <see cref="VictoriaLogMapper" /> — LogsQL query
///     construction, log level normalization, and id length validation.
/// </summary>
public sealed class VictoriaLogMapperTests
{
    private const string FilterText = "service:checkout-api";
    private const string TraceIdText = "abc123def456";
    private const string ExpectedTraceIdFilter = "_trace_id:\"abc123def456\"";

    /// <summary>
    ///     A trace-id filter takes precedence over the explicit filter string
    ///     (LogsQL cannot combine arbitrary filters with trace_id this way).
    /// </summary>
    [Fact]
    public void BuildLogsQuery_WithTraceId_FormatsTraceIdFilter()
    {
        var query = new LogQuery(
            TraceId: new TraceId(TraceIdText),
            Stream: null,
            StartUnixMs: null,
            EndUnixMs: null,
            Filter: null,
            Limit: 100);

        Assert.Equal(ExpectedTraceIdFilter, VictoriaLogMapper.BuildLogsQuery(query));
    }

    /// <summary>
    ///     A free-form filter is used when no trace id is supplied.
    /// </summary>
    [Fact]
    public void BuildLogsQuery_WithFilter_UsesFilter()
    {
        var query = new LogQuery(
            TraceId: null,
            Stream: null,
            StartUnixMs: null,
            EndUnixMs: null,
            Filter: FilterText,
            Limit: 100);

        Assert.Equal(FilterText, VictoriaLogMapper.BuildLogsQuery(query));
    }

    /// <summary>
    ///     No filter or trace id → LogsQL "*".
    /// </summary>
    [Fact]
    public void BuildLogsQuery_NoInputs_ReturnsWildcard()
    {
        var query = new LogQuery(
            TraceId: null, Stream: null, StartUnixMs: null, EndUnixMs: null, Filter: null, Limit: 100);

        Assert.Equal("*", VictoriaLogMapper.BuildLogsQuery(query));
    }

    /// <summary>
    ///     Standard level strings map to the kernel's <see cref="LogLevel" />.
    ///     Unknown / missing values default to <see cref="LogLevel.Information" />.
    /// </summary>
    [Theory]
    [InlineData("ERROR", LogLevel.Error)]
    [InlineData("WARN", LogLevel.Warning)]
    [InlineData("WARNING", LogLevel.Warning)]
    [InlineData("INFO", LogLevel.Information)]
    [InlineData("INFORMATION", LogLevel.Information)]
    [InlineData("DEBUG", LogLevel.Debug)]
    [InlineData("TRACE", LogLevel.Trace)]
    [InlineData("FATAL", LogLevel.Fatal)]
    [InlineData("CRITICAL", LogLevel.Fatal)]
    [InlineData("garbage", LogLevel.Information)]
    [InlineData(null, LogLevel.Information)]
    public void ToLogLevel_MapsCommonValues(string? input, LogLevel expected)
    {
        Assert.Equal(expected, VictoriaLogMapper.ToLogLevel(input));
    }

    /// <summary>
    ///     Valid hex strings (8-16 chars) parse to <see cref="SpanId" />.
    /// </summary>
    [Theory]
    [InlineData("01234567", "01234567")]
    [InlineData("0123456789abcdef", "0123456789abcdef")]
    public void ToSpanId_ValidLength_ReturnsSpanId(string input, string expected)
    {
        var spanId = VictoriaLogMapper.ToSpanId(input);
        Assert.NotNull(spanId);
        Assert.Equal(expected, spanId!.Value);
    }

    /// <summary>
    ///     Empty / too-short / too-long strings return null.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("oversixteenchars_longenough")]
    public void ToSpanId_InvalidLength_ReturnsNull(string input)
    {
        Assert.Null(VictoriaLogMapper.ToSpanId(input));
    }
}