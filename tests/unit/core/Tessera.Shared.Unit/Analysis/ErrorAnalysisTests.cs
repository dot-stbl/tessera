using Tessera.Shared.Kernel.Analysis.Errors;
using Tessera.Shared.Kernel.Domain.Resources;
using Tessera.Shared.Kernel.Domain.Spans;
using Tessera.Shared.Kernel.Domain.Traces;
using Tessera.Shared.Kernel.Identifiers;
using Tessera.Shared.Kernel.Observability;
using Xunit;

namespace Tessera.Shared.Unit.Analysis;

/// <summary>
///     Pure error derivation tests for <see cref="ErrorAnalysis" />.
/// </summary>
public sealed class ErrorAnalysisTests
{
    private static readonly TraceId AnyTraceId = new("0123456789abcdef0123456789abcdef");
    private static readonly SpanId SpanA = new("aaaaaaaaaaaaaaaa");
    private static readonly SpanId SpanB = new("bbbbbbbbbbbbbbbb");

    private static readonly Resource EmptyResource = new(
        ServiceName: "api",
        ServiceNamespace: null,
        DeploymentEnvironment: null,
        Attributes: new Dictionary<string, string>());

    private static Span MakeSpan(
        SpanId id,
        TraceStatus status,
        IReadOnlyList<SpanEvent>? events = null,
        string service = "api",
        string operation = "GET /orders")
    {
        return new Span(
            id,
            ParentSpanId: null,
            Service: service,
            Operation: operation,
            StartTime: 1_000L,
            DurationMs: 50L,
            Status: status,
            Kind: SpanKind.Server,
            Resource: EmptyResource,
            Tags: new Dictionary<string, string>(),
            Events: events ?? []);
    }

    private static SpanEvent MakeExceptionEvent(
        string? type = "System.InvalidOperationException",
        string? message = "boom",
        string? stacktrace = "at Foo()")
    {
        var attributes = new Dictionary<string, string>();
        if (type is not null)
        {
            attributes[SemanticConventions.ExceptionType] = type;
        }

        if (message is not null)
        {
            attributes[SemanticConventions.ExceptionMessage] = message;
        }

        if (stacktrace is not null)
        {
            attributes[SemanticConventions.ExceptionStacktrace] = stacktrace;
        }

        return new SpanEvent(Time: 1_010L, Name: ErrorAnalysis.ExceptionEventName, Attributes: attributes);
    }

    private static TraceDetail MakeTrace(params Span[] spans)
    {
        return new TraceDetail(
            AnyTraceId,
            RootService: "api",
            RootOperation: "GET /orders",
            StartTime: 1_000L,
            DurationMs: 100L,
            Status: TraceStatus.Error,
            Spans: spans);
    }

    /// <inheritdoc/>
    [Fact]
    public void IsErrorSpan_ErrorStatus_ReturnsTrue()
    {
        Assert.True(ErrorAnalysis.IsErrorSpan(MakeSpan(SpanA, TraceStatus.Error)));
        Assert.False(ErrorAnalysis.IsErrorSpan(MakeSpan(SpanA, TraceStatus.Ok)));
        Assert.False(ErrorAnalysis.IsErrorSpan(MakeSpan(SpanA, TraceStatus.Unset)));
    }

    /// <inheritdoc/>
    [Fact]
    public void TryExtractException_ExceptionEvent_ReturnsPayload()
    {
        var span = MakeSpan(SpanA, TraceStatus.Error, [MakeExceptionEvent()]);

        var payload = ErrorAnalysis.TryExtractException(span);

        Assert.NotNull(payload);
        Assert.Equal("System.InvalidOperationException", payload.Type);
        Assert.Equal("boom", payload.Message);
        Assert.Equal("at Foo()", payload.Stacktrace);
    }

    /// <inheritdoc/>
    [Fact]
    public void TryExtractException_NoExceptionEvent_ReturnsNull()
    {
        var span = MakeSpan(
            SpanA,
            TraceStatus.Error,
            [new SpanEvent(1L, "message", new Dictionary<string, string> { ["body"] = "x" })]);

        Assert.Null(ErrorAnalysis.TryExtractException(span));
    }

    /// <inheritdoc/>
    [Fact]
    public void TryExtractException_EmptyAttributes_ReturnsNull()
    {
        var span = MakeSpan(
            SpanA,
            TraceStatus.Error,
            [new SpanEvent(1L, ErrorAnalysis.ExceptionEventName, new Dictionary<string, string>())]);

        Assert.Null(ErrorAnalysis.TryExtractException(span));
    }

    /// <inheritdoc/>
    [Theory]
    [InlineData("order 42 failed", "order {n} failed")]
    [InlineData(
        "id 550e8400-e29b-41d4-a716-446655440000 missing",
        "id {uuid} missing")]
    [InlineData("trace a1b2c3d4e5f67890 not found", "trace {hex} not found")]
    [InlineData("", "")]
    public void NormalizeMessage_ReplacesVolatileTokens(string input, string expected)
    {
        Assert.Equal(expected, ErrorAnalysis.NormalizeMessage(input));
    }

    /// <inheritdoc/>
    [Fact]
    public void BuildGroupKey_JoinsTypeAndNormalizedMessage()
    {
        Assert.Equal(
            "System.Exception|order {n} failed",
            ErrorAnalysis.BuildGroupKey("System.Exception", "order {n} failed"));
        Assert.Equal("|msg", ErrorAnalysis.BuildGroupKey(null, "msg"));
        Assert.Equal("Type|", ErrorAnalysis.BuildGroupKey("Type", null));
        Assert.Equal("|", ErrorAnalysis.BuildGroupKey(null, null));
    }

    /// <inheritdoc/>
    [Fact]
    public void CollectErrors_ErrorSpansWithAndWithoutException()
    {
        var withEx = MakeSpan(SpanA, TraceStatus.Error, [MakeExceptionEvent()], "checkout", "POST /pay");
        var withoutEx = MakeSpan(SpanB, TraceStatus.Error, service: "api", operation: "GET /x");
        var ok = MakeSpan(new SpanId("cccccccccccccccc"), TraceStatus.Ok);
        var trace = MakeTrace(withEx, withoutEx, ok);

        var errors = ErrorAnalysis.CollectErrors(trace);

        Assert.Equal(2, errors.Count);
        Assert.Equal(SpanA, errors[0].SpanId);
        Assert.Equal("checkout", errors[0].Service);
        Assert.Equal("POST /pay", errors[0].Operation);
        Assert.Equal("System.InvalidOperationException", errors[0].ExceptionType);
        Assert.Equal("boom", errors[0].ExceptionMessage);
        Assert.Equal(SpanB, errors[1].SpanId);
        Assert.Null(errors[1].ExceptionType);
    }

    /// <inheritdoc/>
    [Fact]
    public void CollectErrors_NullOrEmpty_ReturnsEmpty()
    {
        Assert.Empty(ErrorAnalysis.CollectErrors(null));
        Assert.Empty(ErrorAnalysis.CollectErrors(MakeTrace()));
    }

    /// <inheritdoc/>
    [Fact]
    public void CountErroredSpans_CountsErrorStatusOnly()
    {
        var trace = MakeTrace(
            MakeSpan(SpanA, TraceStatus.Error),
            MakeSpan(SpanB, TraceStatus.Ok),
            MakeSpan(new SpanId("cccccccccccccccc"), TraceStatus.Error));

        Assert.Equal(2, ErrorAnalysis.CountErroredSpans(trace));
        Assert.Equal(0, ErrorAnalysis.CountErroredSpans(null));
        Assert.Equal(0, ErrorAnalysis.CountErroredSpans(MakeTrace()));
    }

    /// <inheritdoc/>
    [Fact]
    public void SameExceptionDifferentIds_ShareGroupKey()
    {
        var messageA = ErrorAnalysis.NormalizeMessage("failed for user 123");
        var messageB = ErrorAnalysis.NormalizeMessage("failed for user 999");
        var keyA = ErrorAnalysis.BuildGroupKey("System.Exception", messageA);
        var keyB = ErrorAnalysis.BuildGroupKey("System.Exception", messageB);

        Assert.Equal(keyA, keyB);
        Assert.Equal("System.Exception|failed for user {n}", keyA);
    }
}
