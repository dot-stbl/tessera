using Tessera.Modules.Traces.Mapping;
using Tessera.Shared.Kernel.Analysis;
using Tessera.Shared.Kernel.Domain.Resources;
using Tessera.Shared.Kernel.Domain.Spans;
using Tessera.Shared.Kernel.Domain.Traces;
using Tessera.Shared.Kernel.Identifiers;
using Tessera.Shared.Kernel.Observability;

namespace Tessera.Modules.Traces.Unit.Mapping;

/// <summary>
///     <see cref="TracesMapper" /> populates error fields on GetTraceResponse.
/// </summary>
public sealed class TracesMapperTests
{
    private static readonly TraceId AnyTraceId = new("0123456789abcdef0123456789abcdef");
    private static readonly SpanId SpanA = new("aaaaaaaaaaaaaaaa");

    private static readonly Resource EmptyResource = new(
        ServiceName: "api",
        ServiceNamespace: null,
        DeploymentEnvironment: null,
        Attributes: new Dictionary<string, string>());

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
    }

    /// <inheritdoc/>
    [Fact]
    public void ToResponse_LogsOnly_ZeroErrors()
    {
        var view = new RequestView(null, [], RequestViewMode.LogsOnly, []);
        var mapper = new TracesMapper();

        var response = mapper.ToResponse(view);

        Assert.Equal(0, response.ErroredSpanCount);
        Assert.Empty(response.Exceptions);
    }
}
