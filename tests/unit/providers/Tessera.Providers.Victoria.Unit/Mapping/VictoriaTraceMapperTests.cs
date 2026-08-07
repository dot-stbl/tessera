using Tessera.Providers.Victoria.Dto.Jaeger;
using Tessera.Providers.Victoria.Dto.Jaeger.Span;
using Tessera.Providers.Victoria.Dto.Jaeger.Trace;
using Tessera.Providers.Victoria.Implementation.Mapping;
using Tessera.Shared.Kernel.Domain.Spans;
using Tessera.Shared.Kernel.Domain.Traces;
using Tessera.Shared.Kernel.Observability;
using Xunit;

namespace Tessera.Providers.Victoria.Unit.Mapping;

/// <summary>
///     Unit tests for the Jaeger → domain mapping in <see cref="VictoriaTraceMapper" />.
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
                StartTime: 1_000_000,
                Duration: 5_000_000,
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
        Assert.Equal(1000, summary.StartTime);
        Assert.Equal(5000, summary.DurationMs);
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
    ///     HTTP status ≥500 maps to error when otel.status_code is absent.
    /// </summary>
    [Fact]
    public void ToStatus_Http500_MapsToError()
    {
        var span = new JaegerSpan(
            TraceID: "t",
            SpanID: "s",
            OperationName: "op",
            ProcessID: "p1",
            StartTime: 0,
            Duration: 0,
            Tags: [new JaegerTag(SemanticConventions.HttpResponseStatusCode, "int64", "503")],
            Logs: [],
            References: []);

        Assert.Equal(TraceStatus.Error, VictoriaTraceMapper.ToStatus(span));
    }

    /// <summary>
    ///     gRPC status ≠0 maps to error.
    /// </summary>
    [Fact]
    public void ToStatus_GrpcNonZero_MapsToError()
    {
        var span = new JaegerSpan(
            TraceID: "t",
            SpanID: "s",
            OperationName: "op",
            ProcessID: "p1",
            StartTime: 0,
            Duration: 0,
            Tags: [new JaegerTag(SemanticConventions.RpcGrpcStatusCode, "int64", "13")],
            Logs: [],
            References: []);

        Assert.Equal(TraceStatus.Error, VictoriaTraceMapper.ToStatus(span));
    }

    /// <summary>
    ///     otel.status_code ERROR wins over a non-error HTTP code.
    /// </summary>
    [Fact]
    public void ToStatus_OtelError_WinsOverHttp200()
    {
        var span = new JaegerSpan(
            TraceID: "t",
            SpanID: "s",
            OperationName: "op",
            ProcessID: "p1",
            StartTime: 0,
            Duration: 0,
            Tags:
            [
                new JaegerTag(SemanticConventions.OtelStatusCode, "string", "ERROR"),
                new JaegerTag(SemanticConventions.HttpResponseStatusCode, "int64", "200"),
            ],
            Logs: [],
            References: []);

        Assert.Equal(TraceStatus.Error, VictoriaTraceMapper.ToStatus(span));
    }

    /// <summary>
    ///     span.kind tag maps to <see cref="SpanKind" />.
    /// </summary>
    [Theory]
    [InlineData("server", SpanKind.Server)]
    [InlineData("client", SpanKind.Client)]
    [InlineData("internal", SpanKind.Internal)]
    [InlineData("producer", SpanKind.Producer)]
    [InlineData("consumer", SpanKind.Consumer)]
    [InlineData("SERVER", SpanKind.Server)]
    public void ToSpanKind_MapsKnownValues(string raw, SpanKind expected)
    {
        var tags = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [SemanticConventions.SpanKind] = raw,
        };

        Assert.Equal(expected, VictoriaTraceMapper.ToSpanKind(tags));
    }

    /// <summary>
    ///     Process tags become a Resource; one instance is shared per process id.
    /// </summary>
    [Fact]
    public void ToDomainSpan_InternsResourcePerProcessId()
    {
        var processes = new Dictionary<string, JaegerProcess>
        {
            ["p1"] = new JaegerProcess(
                "checkout-api",
                [
                    new JaegerTag(SemanticConventions.ServiceNamespace, "string", "shop"),
                    new JaegerTag(SemanticConventions.DeploymentEnvironment, "string", "prod"),
                    new JaegerTag("host.name", "string", "node-1"),
                ]),
        };
        var resources = VictoriaTraceMapper.InternResources(processes);

        var spanA = new JaegerSpan(
            "t", "s1", "op-a", "p1", 2_000_000, 1_000_000,
            [new JaegerTag(SemanticConventions.SpanKind, "string", "server")],
            [],
            []);
        var spanB = new JaegerSpan(
            "t", "s2", "op-b", "p1", 3_000_000, 500_000,
            [new JaegerTag(SemanticConventions.SpanKind, "string", "client")],
            [],
            []);

        var domainA = VictoriaTraceMapper.ToDomainSpan(spanA, resources);
        var domainB = VictoriaTraceMapper.ToDomainSpan(spanB, resources);

        Assert.Same(domainA.Resource, domainB.Resource);
        Assert.Equal("checkout-api", domainA.Service);
        Assert.Equal("checkout-api", domainA.Resource.ServiceName);
        Assert.Equal("shop", domainA.Resource.ServiceNamespace);
        Assert.Equal("prod", domainA.Resource.DeploymentEnvironment);
        Assert.Equal("node-1", domainA.Resource.Attributes["host.name"]);
        Assert.Equal(SpanKind.Server, domainA.Kind);
        Assert.Equal(SpanKind.Client, domainB.Kind);
        Assert.Equal(2000, domainA.StartTime);
        Assert.Equal(1000, domainA.DurationMs);
    }

    /// <summary>
    ///     Exception log fields force event name "exception"; timestamp µs→ms.
    /// </summary>
    [Fact]
    public void ToSpanEvent_ExceptionAttributes_NamedException()
    {
        var log = new JaegerLog(
            Timestamp: 5_000_000,
            Fields:
            [
                new JaegerTag(SemanticConventions.ExceptionType, "string", "System.Exception"),
                new JaegerTag(SemanticConventions.ExceptionMessage, "string", "boom"),
            ]);

        var spanEvent = VictoriaTraceMapper.ToSpanEvent(log);

        Assert.Equal("exception", spanEvent.Name);
        Assert.Equal(5000, spanEvent.Time);
        Assert.Equal("System.Exception", spanEvent.Attributes[SemanticConventions.ExceptionType]);
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
