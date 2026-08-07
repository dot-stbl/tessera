using Tessera.Providers.Victoria.Dto.VictoriaLogs;
using Tessera.Providers.Victoria.Implementation.Mapping;
using Tessera.Shared.Kernel.Identifiers;
using Tessera.Shared.Kernel.Observability;
using Tessera.Shared.Kernel.Providers.Logs;
using Tessera.Shared.Kernel.Time;
using Xunit;

namespace Tessera.Providers.Victoria.Unit.Mapping;

/// <summary>
///     Unit tests for <see cref="VictoriaLogMapper" /> — LogsQL query
///     construction, severity mapping, and service / trace id resolution.
/// </summary>
public sealed class VictoriaLogMapperTests
{
    private const string FilterText = "service:checkout-api";
    private const string TraceIdText = "abc123def4567890";
    private const string ExpectedTraceIdFilter = "_trace_id:\"abc123def4567890\"";

    /// <summary>
    ///     A trace-id filter takes precedence over the explicit filter string
    ///     and uses the VL <c>_trace_id</c> field.
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
    /// </summary>
    [Theory]
    [InlineData("ERROR", LogLevel.Error)]
    [InlineData("WARN", LogLevel.Warn)]
    [InlineData("WARNING", LogLevel.Warn)]
    [InlineData("INFO", LogLevel.Info)]
    [InlineData("INFORMATION", LogLevel.Info)]
    [InlineData("DEBUG", LogLevel.Debug)]
    [InlineData("TRACE", LogLevel.Trace)]
    [InlineData("FATAL", LogLevel.Fatal)]
    [InlineData("CRITICAL", LogLevel.Fatal)]
    [InlineData("garbage", LogLevel.Info)]
    [InlineData(null, LogLevel.Info)]
    public void ToLogLevel_MapsCommonValues(string? input, LogLevel expected)
    {
        Assert.Equal(expected, VictoriaLogMapper.ToLogLevel(input));
    }

    /// <summary>
    ///     OTel severity_number bands map to kernel log levels.
    /// </summary>
    [Theory]
    [InlineData(1, LogLevel.Trace)]
    [InlineData(5, LogLevel.Debug)]
    [InlineData(9, LogLevel.Info)]
    [InlineData(13, LogLevel.Warn)]
    [InlineData(17, LogLevel.Error)]
    [InlineData(21, LogLevel.Fatal)]
    public void FromSeverityNumber_MapsBands(int number, LogLevel expected)
    {
        Assert.Equal(expected, VictoriaLogMapper.FromSeverityNumber(number));
    }

    /// <summary>
    ///     severity_number wins over text level.
    /// </summary>
    [Fact]
    public void ResolveLogLevel_PrefersSeverityNumber()
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["severity_number"] = "17",
            ["level"] = "INFO",
        };

        Assert.Equal(LogLevel.Error, VictoriaLogMapper.ResolveLogLevel(fields));
    }

    /// <summary>
    ///     Service comes from resource service.name, not _stream, when present.
    /// </summary>
    [Fact]
    public void ResolveService_PrefersServiceNameAttribute()
    {
        var dto = new VLLogEntry(
            Msg: "hi",
            Stream: "{app=\"stream-label\"}",
            Time: DateTimeOffset.UnixEpoch,
            Fields: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [SemanticConventions.ServiceName] = "checkout-api",
            });

        Assert.Equal("checkout-api", VictoriaLogMapper.ResolveService(dto));
    }

    /// <summary>
    ///     Parse accepts both <c>_trace_id</c> and <c>trace_id</c>.
    /// </summary>
    [Fact]
    public void ReadTraceId_AcceptsUnderscoreAndPlain()
    {
        var underscored = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["_trace_id"] = "0123456789abcdef",
        };
        var plain = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["trace_id"] = "fedcba9876543210",
        };

        Assert.Equal("0123456789abcdef", VictoriaLogMapper.ReadTraceId(underscored)!.Value);
        Assert.Equal("fedcba9876543210", VictoriaLogMapper.ReadTraceId(plain)!.Value);
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
        Assert.Equal(expected, spanId.Value);
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
