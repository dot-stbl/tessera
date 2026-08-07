using Tessera.Shared.Kernel.Analysis;
using Tessera.Shared.Kernel.Analysis.Assembly;
using Tessera.Shared.Kernel.Domain.Logs;
using Tessera.Shared.Kernel.Domain.Resources;
using Tessera.Shared.Kernel.Domain.Spans;
using Tessera.Shared.Kernel.Domain.Traces;
using Tessera.Shared.Kernel.Identifiers;
using Tessera.Shared.Kernel.Time;
using Xunit;

namespace Tessera.Shared.Unit.Analysis;

/// <summary>
///     Pure request-view derivation tests for
///     <see cref="RequestViewAnalysis" />.
/// </summary>
public sealed class RequestViewAnalysisTests
{
    private static readonly TraceId AnyTraceId = new("0123456789abcdef0123456789abcdef");
    private static readonly SpanId SpanA = new("aaaaaaaaaaaaaaaa");
    private static readonly SpanId SpanB = new("bbbbbbbbbbbbbbbb");
    private static readonly SpanId OrphanSpan = new("cccccccccccccccc");

    private static readonly Resource EmptyResource = new(
        ServiceName: "api",
        ServiceNamespace: null,
        DeploymentEnvironment: null,
        Attributes: new Dictionary<string, string>());

    private static readonly IReadOnlyDictionary<string, string> EmptyFields =
        new Dictionary<string, string>();

    private static Span MakeSpan(SpanId id, long startTime, TraceStatus status = TraceStatus.Ok)
    {
        return new Span(
            id,
            ParentSpanId: null,
            Service: "api",
            Operation: "op",
            StartTime: startTime,
            DurationMs: 50L,
            Status: status,
            Kind: SpanKind.Internal,
            Resource: EmptyResource,
            Tags: new Dictionary<string, string>(),
            Events: []);
    }

    private static TraceDetail MakeTrace(params Span[] spans)
    {
        return new TraceDetail(
            AnyTraceId,
            RootService: "api",
            RootOperation: "GET /x",
            StartTime: 1_000L,
            DurationMs: 500L,
            Status: TraceStatus.Ok,
            Spans: spans);
    }

    private static LogEntry MakeLog(long timestamp, SpanId? spanId, string message = "msg")
    {
        return new LogEntry(
            Timestamp: timestamp,
            Level: LogLevel.Info,
            Service: "api",
            TraceId: AnyTraceId,
            SpanId: spanId,
            Message: message,
            Fields: EmptyFields);
    }

    /// <inheritdoc/>
    [Fact]
    public void Classify_SpansAndLogs_ReturnsFull()
    {
        var trace = MakeTrace(MakeSpan(SpanA, 1_000L));
        IReadOnlyList<LogEntry> logs = [MakeLog(1_010L, SpanA)];

        var mode = RequestViewAnalysis.Classify(trace, logs);

        Assert.Equal(RequestViewMode.Full, mode);
    }

    /// <inheritdoc/>
    [Fact]
    public void Classify_SpansOnly_ReturnsSpansOnly()
    {
        var trace = MakeTrace(MakeSpan(SpanA, 1_000L));

        var mode = RequestViewAnalysis.Classify(trace, []);

        Assert.Equal(RequestViewMode.SpansOnly, mode);
    }

    /// <inheritdoc/>
    [Fact]
    public void Classify_TraceWithEmptySpansAndLogs_ReturnsLogsOnly()
    {
        var trace = MakeTrace();
        IReadOnlyList<LogEntry> logs = [MakeLog(1_010L, null)];

        var mode = RequestViewAnalysis.Classify(trace, logs);

        Assert.Equal(RequestViewMode.LogsOnly, mode);
    }

    /// <inheritdoc/>
    [Fact]
    public void Classify_NullTraceWithLogs_ReturnsLogsOnly()
    {
        IReadOnlyList<LogEntry> logs = [MakeLog(1_010L, null)];

        var mode = RequestViewAnalysis.Classify(null, logs);

        Assert.Equal(RequestViewMode.LogsOnly, mode);
    }

    /// <inheritdoc/>
    [Fact]
    public void Classify_BothEmpty_ReturnsEmpty()
    {
        Assert.Equal(RequestViewMode.Empty, RequestViewAnalysis.Classify(null, []));
        Assert.Equal(RequestViewMode.Empty, RequestViewAnalysis.Classify(MakeTrace(), []));
    }

    /// <inheritdoc/>
    [Fact]
    public void BuildMarkers_MatchingSpan_OffsetsFromSpanStart()
    {
        const long spanStart = 2_000L;
        const long logTime = 2_050L;
        var trace = MakeTrace(MakeSpan(SpanA, spanStart));
        IReadOnlyList<LogEntry> logs = [MakeLog(logTime, SpanA, "on-span")];

        var markers = RequestViewAnalysis.BuildMarkers(trace, logs);

        Assert.Single(markers);
        Assert.Equal(SpanA, markers[0].SpanId);
        Assert.Equal(logTime - spanStart, markers[0].OffsetMs);
        Assert.Equal(logTime, markers[0].TimestampUnixMs);
        Assert.Equal("on-span", markers[0].Message);
    }

    /// <inheritdoc/>
    [Fact]
    public void BuildMarkers_NoSpanId_OffsetsFromTraceStart()
    {
        var trace = MakeTrace(MakeSpan(SpanA, 2_000L));
        IReadOnlyList<LogEntry> logs = [MakeLog(1_100L, null, "trace-level")];

        var markers = RequestViewAnalysis.BuildMarkers(trace, logs);

        Assert.Single(markers);
        Assert.Null(markers[0].SpanId);
        Assert.Equal(1_100L - trace.StartTime, markers[0].OffsetMs);
    }

    /// <inheritdoc/>
    [Fact]
    public void BuildMarkers_OrphanSpanId_OffsetsFromTraceStart()
    {
        var trace = MakeTrace(MakeSpan(SpanA, 2_000L));
        IReadOnlyList<LogEntry> logs = [MakeLog(1_150L, OrphanSpan)];

        var markers = RequestViewAnalysis.BuildMarkers(trace, logs);

        Assert.Single(markers);
        Assert.Null(markers[0].SpanId);
        Assert.Equal(1_150L - trace.StartTime, markers[0].OffsetMs);
    }

    /// <inheritdoc/>
    [Fact]
    public void BuildMarkers_LogsOnly_OffsetsFromEarliestLog()
    {
        IReadOnlyList<LogEntry> logs =
        [
            MakeLog(5_000L, SpanA, "second"),
            MakeLog(4_000L, null, "first"),
        ];

        var markers = RequestViewAnalysis.BuildMarkers(null, logs);

        Assert.Equal(2, markers.Count);
        Assert.Equal(1_000L, markers[0].OffsetMs);
        Assert.Equal(0L, markers[1].OffsetMs);
        Assert.Null(markers[0].SpanId);
        Assert.Null(markers[1].SpanId);
    }

    /// <inheritdoc/>
    [Fact]
    public void BuildMarkers_EmptyLogs_ReturnsEmpty()
    {
        var markers = RequestViewAnalysis.BuildMarkers(MakeTrace(MakeSpan(SpanA, 1L)), []);

        Assert.Empty(markers);
    }

    /// <inheritdoc/>
    [Fact]
    public void GroupLogsBySpan_GroupsBySpanId_OmitsTraceLevel()
    {
        IReadOnlyList<LogEntry> logs =
        [
            MakeLog(1L, SpanA, "a1"),
            MakeLog(2L, SpanA, "a2"),
            MakeLog(3L, SpanB, "b1"),
            MakeLog(4L, null, "trace"),
        ];

        var groups = RequestViewAnalysis.GroupLogsBySpan(logs);

        Assert.Equal(2, groups.Count);
        Assert.Equal(2, groups[SpanA.Value].Count);
        Assert.Single(groups[SpanB.Value]);
        Assert.False(groups.ContainsKey(OrphanSpan.Value));
    }

    /// <inheritdoc/>
    [Fact]
    public void CountErroredSpans_CountsErrorStatusOnly()
    {
        var trace = MakeTrace(
            MakeSpan(SpanA, 1L, TraceStatus.Error),
            MakeSpan(SpanB, 2L, TraceStatus.Ok),
            MakeSpan(OrphanSpan, 3L, TraceStatus.Error));

        Assert.Equal(2, RequestViewAnalysis.CountErroredSpans(trace));
        Assert.Equal(0, RequestViewAnalysis.CountErroredSpans(null));
        Assert.Equal(0, RequestViewAnalysis.CountErroredSpans(MakeTrace()));
    }

    /// <inheritdoc/>
    [Fact]
    public void Build_AssemblesModeAndMarkers()
    {
        var trace = MakeTrace(MakeSpan(SpanA, 1_000L));
        IReadOnlyList<LogEntry> logs = [MakeLog(1_010L, SpanA)];

        var view = RequestViewAnalysis.Build(trace, logs);

        Assert.Equal(RequestViewMode.Full, view.Mode);
        Assert.Same(trace, view.Trace);
        Assert.Single(view.Logs);
        Assert.Single(view.Markers);
    }
}
