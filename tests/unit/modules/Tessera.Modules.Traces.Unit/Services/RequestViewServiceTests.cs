using NSubstitute;
using Tessera.Modules.Traces.Errors;
using Tessera.Modules.Traces.Services;
using Tessera.Shared.Kernel.Analysis;
using Tessera.Shared.Kernel.Domain.Logs;
using Tessera.Shared.Kernel.Domain.Resources;
using Tessera.Shared.Kernel.Domain.Spans;
using Tessera.Shared.Kernel.Domain.Traces;
using Tessera.Shared.Kernel.Exceptions;
using Tessera.Shared.Kernel.Identifiers;
using Tessera.Shared.Kernel.Providers.Logs;
using Tessera.Shared.Kernel.Providers.Traces;
using Tessera.Shared.Kernel.Time;

namespace Tessera.Modules.Traces.Unit.Services;

/// <summary>
///     <see cref="RequestViewService" /> orchestration — degradable
///     correlation with NSubstitute provider fakes.
/// </summary>
public sealed class RequestViewServiceTests
{
    private static readonly TraceId AnyTraceId = new("0123456789abcdef0123456789abcdef");
    private static readonly SpanId SpanA = new("aaaaaaaaaaaaaaaa");

    private static readonly Resource EmptyResource = new(
        ServiceName: "api",
        ServiceNamespace: null,
        DeploymentEnvironment: null,
        Attributes: new Dictionary<string, string>());

    private static readonly IReadOnlyDictionary<string, string> EmptyFields =
        new Dictionary<string, string>();

    private static readonly TimeProvider FixedClock =
        new FixedTimeProvider(new DateTimeOffset(2026, 8, 7, 12, 0, 0, TimeSpan.Zero));

    private static TraceDetail MakeTrace()
    {
        var span = new Span(
            SpanA,
            ParentSpanId: null,
            Service: "api",
            Operation: "GET /orders",
            StartTime: 1_000L,
            DurationMs: 100L,
            Status: TraceStatus.Ok,
            Kind: SpanKind.Server,
            Resource: EmptyResource,
            Tags: new Dictionary<string, string>(),
            Events: []);

        return new TraceDetail(
            AnyTraceId,
            "api-gateway",
            "GET /orders",
            StartTime: 1_000L,
            DurationMs: 100L,
            Status: TraceStatus.Ok,
            Spans: [span]);
    }

    private static LogEntry MakeLog()
    {
        return new LogEntry(
            Timestamp: 1_050L,
            Level: LogLevel.Info,
            Service: "api",
            TraceId: AnyTraceId,
            SpanId: SpanA,
            Message: "hello",
            Fields: EmptyFields);
    }

    /// <inheritdoc/>
    [Fact]
    public async Task GetAsync_SpansAndLogs_ReturnsFull()
    {
        var traceProvider = Substitute.For<ITraceProvider>();
        var logProvider = Substitute.For<ILogProvider>();
        var trace = MakeTrace();
        IReadOnlyList<LogEntry> logs = [MakeLog()];
        traceProvider.GetByIdAsync(AnyTraceId, Arg.Any<CancellationToken>()).Returns(trace);
        logProvider
            .ListByTraceAsync(AnyTraceId, Arg.Any<TimeRange>(), Arg.Any<CancellationToken>())
            .Returns(logs);
        var service = new RequestViewService(traceProvider, logProvider, FixedClock);

        var view = await service.GetAsync(AnyTraceId);

        Assert.Equal(RequestViewMode.Full, view.Mode);
        Assert.Same(trace, view.Trace);
        Assert.Single(view.Logs);
        Assert.Single(view.Markers);
    }

    /// <inheritdoc/>
    [Fact]
    public async Task GetAsync_SpansOnly_ReturnsSpansOnly()
    {
        var traceProvider = Substitute.For<ITraceProvider>();
        var logProvider = Substitute.For<ILogProvider>();
        var trace = MakeTrace();
        traceProvider.GetByIdAsync(AnyTraceId, Arg.Any<CancellationToken>()).Returns(trace);
        logProvider
            .ListByTraceAsync(AnyTraceId, Arg.Any<TimeRange>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<LogEntry>>([]));
        var service = new RequestViewService(traceProvider, logProvider, FixedClock);

        var view = await service.GetAsync(AnyTraceId);

        Assert.Equal(RequestViewMode.SpansOnly, view.Mode);
        Assert.Same(trace, view.Trace);
        Assert.Empty(view.Logs);
    }

    /// <inheritdoc/>
    [Fact]
    public async Task GetAsync_LogsOnly_ReturnsLogsOnlyNot404()
    {
        var traceProvider = Substitute.For<ITraceProvider>();
        var logProvider = Substitute.For<ILogProvider>();
        IReadOnlyList<LogEntry> logs = [MakeLog()];
        traceProvider.GetByIdAsync(AnyTraceId, Arg.Any<CancellationToken>()).Returns((TraceDetail?)null);
        logProvider
            .ListByTraceAsync(AnyTraceId, Arg.Any<TimeRange>(), Arg.Any<CancellationToken>())
            .Returns(logs);
        var service = new RequestViewService(traceProvider, logProvider, FixedClock);

        var view = await service.GetAsync(AnyTraceId);

        Assert.Equal(RequestViewMode.LogsOnly, view.Mode);
        Assert.Null(view.Trace);
        Assert.Single(view.Logs);
        await logProvider.Received(1).ListByTraceAsync(
            AnyTraceId,
            Arg.Is<TimeRange>(static range =>
                range.EndUnixMs - range.StartUnixMs == (long)TimeSpan.FromHours(24).TotalMilliseconds),
            Arg.Any<CancellationToken>());
    }

    /// <inheritdoc/>
    [Fact]
    public async Task GetAsync_BothEmpty_ThrowsProviderNotFound()
    {
        var traceProvider = Substitute.For<ITraceProvider>();
        var logProvider = Substitute.For<ILogProvider>();
        traceProvider.GetByIdAsync(AnyTraceId, Arg.Any<CancellationToken>()).Returns((TraceDetail?)null);
        logProvider
            .ListByTraceAsync(AnyTraceId, Arg.Any<TimeRange>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<LogEntry>>([]));
        var service = new RequestViewService(traceProvider, logProvider, FixedClock);

        var exception = await Assert.ThrowsAsync<ProviderNotFoundException>(
            () => service.GetAsync(AnyTraceId));

        Assert.Equal(TracesErrors.TraceNotFound, exception.Code);
        Assert.Contains(AnyTraceId.Value, exception.Message);
    }

    /// <inheritdoc/>
    [Fact]
    public async Task GetAsync_ForwardsCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var traceProvider = Substitute.For<ITraceProvider>();
        var logProvider = Substitute.For<ILogProvider>();
        var trace = MakeTrace();
        traceProvider.GetByIdAsync(AnyTraceId, cts.Token).Returns(trace);
        logProvider
            .ListByTraceAsync(AnyTraceId, Arg.Any<TimeRange>(), cts.Token)
            .Returns(Task.FromResult<IReadOnlyList<LogEntry>>([]));
        var service = new RequestViewService(traceProvider, logProvider, FixedClock);

        await service.GetAsync(AnyTraceId, cts.Token);

        await traceProvider.Received(1).GetByIdAsync(AnyTraceId, cts.Token);
        await logProvider.Received(1).ListByTraceAsync(AnyTraceId, Arg.Any<TimeRange>(), cts.Token);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return now;
        }
    }
}
