using Tessera.Providers.Victoria.Dto.Jaeger;
using Tessera.Providers.Victoria.Dto.Jaeger.Span;
using Tessera.Providers.Victoria.Dto.Jaeger.Trace;
using Tessera.Providers.Victoria.Implementation.Mapping;
using Tessera.Shared.Kernel.Domain.Traces;
using Xunit;

namespace Tessera.Providers.Victoria.Unit.Mapping;

/// <summary>
///     Unit tests for the Jaeger → domain mapping in <see cref="VictoriaTraceMapper" />.
///     The mapper is now <c>internal static</c> (top-level, not private to a class)
///     so we test it directly through its public surface.
/// </summary>
public sealed class VictoriaTraceMapperTests
{
    private static readonly string[] ExpectedServices = ["checkout-api"];

    /// <summary>
    ///     A Jaeger trace envelope with no spans maps to a TraceSummary with
    ///     default service name "unknown" and zero span count.
    /// </summary>
    [Fact]
    public void ToSummary_EmptySpans_ReturnsEmptySummary()
    {
        var trace = new JaegerTrace(
            TraceID: "abc123",
            Spans: [],
            Processes: new Dictionary<string, JaegerProcess>(),
            Warnings: null);

        var summary = VictoriaTraceMapper.ToSummary(trace);

        Assert.Equal("abc123", summary.TraceId.Value);
        Assert.Equal("unknown", summary.RootService);
        Assert.Equal("unknown", summary.RootOperation);
        Assert.Equal(0, summary.SpanCount);
        Assert.Empty(summary.Services);
    }

    /// <summary>
    ///     A trace with one root span (no references) uses that span as the root.
    /// </summary>
    [Fact]
    public void ToSummary_RootSpanWithNoReferences_UsesAsRoot()
    {
        var trace = new JaegerTrace(
            TraceID: "abc",
            Spans: [new JaegerSpan(
                TraceID: "abc",
                SpanID: "s1",
                OperationName: "GET /cart",
                ProcessID: "p1",
                StartTime: 1000,
                Duration: 5000,
                Tags: [new JaegerTag("http.method", "string", "GET")],
                Logs: [],
                References: [])],
            Processes: new Dictionary<string, JaegerProcess>
            {
                ["p1"] = new JaegerProcess("checkout-api", []),
            },
            Warnings: null);

        var summary = VictoriaTraceMapper.ToSummary(trace);

        Assert.Equal("checkout-api", summary.RootService);
        Assert.Equal("GET /cart", summary.RootOperation);
        Assert.Equal(1, summary.SpanCount);
        Assert.Equal(ExpectedServices, summary.Services);
    }

    /// <summary>
    ///     An "error=true" tag on a span maps to <see cref="TraceStatus.Error" />.
    /// </summary>
    [Fact]
    public void ToSummary_ErrorTag_MapsToError()
    {
        var trace = new JaegerTrace(
            TraceID: "abc",
            Spans: [new JaegerSpan(
                TraceID: "abc",
                SpanID: "s1",
                OperationName: "x",
                ProcessID: "p1",
                StartTime: 0,
                Duration: 0,
                Tags: [new JaegerTag("error", "string", "true")],
                Logs: [],
                References: [])],
            Processes: new Dictionary<string, JaegerProcess> { ["p1"] = new JaegerProcess("svc", []) },
            Warnings: null);

        var summary = VictoriaTraceMapper.ToSummary(trace);

        Assert.Equal(TraceStatus.Error, summary.Status);
    }

    /// <summary>
    ///     FormatDuration renders sub-second as "Nms" and multi-second as "Ns".
    /// </summary>
    [Theory]
    [InlineData(null, null)]
    [InlineData(500, "500ms")]
    [InlineData(1000, "1s")]
    [InlineData(5500, "5s")]
    public void FormatDuration_ProducesExpectedForm(int? input, string? expected)
    {
        Assert.Equal(expected, VictoriaTraceMapper.FormatDuration(input));
    }
}