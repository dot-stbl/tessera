using Tessera.Modules.Traces.Mapping;
using Tessera.Shared.Kernel.Analysis;
using Tessera.Shared.Kernel.Analysis.Deps.Models;
using Tessera.Shared.Kernel.Domain.Resources;
using Tessera.Shared.Kernel.Domain.Spans;
using Tessera.Shared.Kernel.Domain.Traces;
using Tessera.Shared.Kernel.Identifiers;
using Tessera.Shared.Kernel.Observability;

namespace Tessera.Modules.Traces.Unit.Mapping;

/// <summary>
///     <see cref="TracesMapper" /> populates error fields and dependency graph
///     on GetTraceResponse.
/// </summary>
public sealed class TracesMapperTests
{
    private static readonly TraceId AnyTraceId = new("0123456789abcdef0123456789abcdef");
    private static readonly SpanId SpanA = new("aaaaaaaaaaaaaaaa");
    private static readonly SpanId SpanB = new("bbbbbbbbbbbbbbbb");

    private static readonly Resource EmptyResource = new(
        ServiceName: "api",
        ServiceNamespace: null,
        DeploymentEnvironment: null,
        Attributes: new Dictionary<string, string>());

    private static Resource MakeResource(string serviceName)
    {
        return new Resource(
            ServiceName: serviceName,
            ServiceNamespace: null,
            DeploymentEnvironment: null,
            Attributes: new Dictionary<string, string>());
    }

    /// <inheritdoc/>
    [Fact]
    public void ToResponse_ErrorSpanWithException_FillsErrorFields()
    {
        var attributes = new Dictionary<string, string>
        {
            [SemanticConventions.ExceptionType] = "System.Exception",
            [SemanticConventions.ExceptionMessage] = "boom",
        };
        var span = new Span(
            SpanA,
            ParentSpanId: null,
            Service: "api",
            Operation: "GET /x",
            StartTime: 1L,
            DurationMs: 10L,
            Status: TraceStatus.Error,
            Kind: SpanKind.Server,
            Resource: EmptyResource,
            Tags: new Dictionary<string, string>(),
            Events: [new SpanEvent(2L, "exception", attributes)]);
        var trace = new TraceDetail(
            AnyTraceId,
            "api",
            "GET /x",
            StartTime: 1L,
            DurationMs: 10L,
            Status: TraceStatus.Error,
            Spans: [span]);
        var view = new RequestView(trace, [], RequestViewMode.SpansOnly, []);
        var mapper = new TracesMapper();

        var response = mapper.ToResponse(view);

        Assert.Equal(1, response.ErroredSpanCount);
        Assert.Single(response.Exceptions);
        Assert.Equal("System.Exception", response.Exceptions[0].ExceptionType);
        Assert.Equal("boom", response.Exceptions[0].ExceptionMessage);
        Assert.Equal(SpanA.Value, response.Exceptions[0].SpanId);
        Assert.Equal("api", response.Exceptions[0].Service);
        Assert.Equal("GET /x", response.Exceptions[0].Operation);
        Assert.NotNull(response.DependencyGraph);
    }

    /// <inheritdoc/>
    [Fact]
    public void ToResponse_LogsOnly_ZeroErrorsAndNullDependencyGraph()
    {
        var view = new RequestView(null, [], RequestViewMode.LogsOnly, []);
        var mapper = new TracesMapper();

        var response = mapper.ToResponse(view);

        Assert.Equal(0, response.ErroredSpanCount);
        Assert.Empty(response.Exceptions);
        Assert.Null(response.DependencyGraph);
    }

    /// <inheritdoc/>
    [Fact]
    public void ToResponse_ClientServerSpans_FillsDependencyGraph()
    {
        var client = new Span(
            SpanA,
            ParentSpanId: null,
            Service: "checkout-api",
            Operation: "call currency",
            StartTime: 1L,
            DurationMs: 20L,
            Status: TraceStatus.Ok,
            Kind: SpanKind.Client,
            Resource: MakeResource("checkout-api"),
            Tags: new Dictionary<string, string>(),
            Events: []);
        var server = new Span(
            SpanB,
            ParentSpanId: SpanA,
            Service: "currency",
            Operation: "GET /convert",
            StartTime: 2L,
            DurationMs: 10L,
            Status: TraceStatus.Ok,
            Kind: SpanKind.Server,
            Resource: MakeResource("currency"),
            Tags: new Dictionary<string, string>(),
            Events: []);
        var trace = new TraceDetail(
            AnyTraceId,
            "checkout-api",
            "POST /checkout",
            StartTime: 1L,
            DurationMs: 30L,
            Status: TraceStatus.Ok,
            Spans: [client, server]);
        var view = new RequestView(trace, [], RequestViewMode.Full, []);
        var mapper = new TracesMapper();

        var response = mapper.ToResponse(view);

        Assert.NotNull(response.DependencyGraph);
        Assert.Single(response.DependencyGraph.Edges);
        Assert.Equal("checkout-api", response.DependencyGraph.Edges[0].FromId);
        Assert.Equal("currency", response.DependencyGraph.Edges[0].ToId);
        Assert.Contains(
            response.DependencyGraph.Nodes,
            static node => node is { Id: "currency", Kind: DependencyNodeKind.Service });
    }
}
