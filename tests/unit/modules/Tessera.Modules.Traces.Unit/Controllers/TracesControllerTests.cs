using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Tessera.Modules.Traces.Contracts;
using Tessera.Modules.Traces.Controllers;
using Tessera.Modules.Traces.Errors;
using Tessera.Modules.Traces.Mapping;
using Tessera.Modules.Traces.Services;
using Tessera.Shared.Kernel.Analysis;
using Tessera.Shared.Kernel.Domain.Logs;
using Tessera.Shared.Kernel.Domain.Resources;
using Tessera.Shared.Kernel.Domain.Spans;
using Tessera.Shared.Kernel.Domain.Traces;
using Tessera.Shared.Kernel.Exceptions;
using Tessera.Shared.Kernel.Identifiers;
using Tessera.Shared.Kernel.Pagination;
using Tessera.Shared.Kernel.Providers.Logs;
using Tessera.Shared.Kernel.Providers.Traces;
using Tessera.Shared.Kernel.Time;

namespace Tessera.Modules.Traces.Unit.Controllers;

/// <summary>
///     <see cref="TracesController" /> orchestration tests — List + Get
///     (via <see cref="RequestViewService" />) with NSubstitute mocks.
/// </summary>
public sealed class TracesControllerTests
{
    private static readonly TraceId AnyTraceId = new("0123456789abcdef0123456789abcdef");

    private static readonly string[] SingleService = ["api"];

    private static readonly Resource EmptyResource = new(
        ServiceName: "api",
        ServiceNamespace: null,
        DeploymentEnvironment: null,
        Attributes: new Dictionary<string, string>());

    private static readonly TimeProvider FixedClock =
        new FixedTimeProvider(new DateTimeOffset(2026, 8, 7, 12, 0, 0, TimeSpan.Zero));

    private static TraceDetail MakeTrace(TraceId? id = null)
    {
        var span = new Span(
            new SpanId("aaaaaaaaaaaaaaaa"),
            ParentSpanId: null,
            Service: "api",
            Operation: "GET /orders",
            StartTime: 0L,
            DurationMs: 100L,
            Status: TraceStatus.Ok,
            Kind: SpanKind.Server,
            Resource: EmptyResource,
            Tags: new Dictionary<string, string>(),
            Events: []);

        return new TraceDetail(
            id ?? AnyTraceId,
            "api-gateway",
            "GET /orders",
            StartTime: 0L,
            DurationMs: 100L,
            Status: TraceStatus.Ok,
            Spans: [span]);
    }

    private static RequestViewService MakeService(
        ITraceProvider traceProvider,
        ILogProvider logProvider)
    {
        return new RequestViewService(traceProvider, logProvider, FixedClock);
    }

    /// <summary>
    ///     <c>ListAsync</c> converts the <see cref="ListTracesRequest" /> via
    ///     its <c>ToTraceSearchQuery()</c> projection before calling the
    ///     provider, then returns the provider's <see cref="Page{T}" />
    ///     wrapped in 200 OK with the page intact.
    /// </summary>
    [Fact]
    public async Task ListAsync_ReturnsProviderPageInOkBody()
    {
        var traceProvider = Substitute.For<ITraceProvider>();
        var logProvider = Substitute.For<ILogProvider>();
        var mapper = Substitute.For<ITracesMapper>();
        var traceSummary = new TraceSummary(
            AnyTraceId,
            "api",
            "GET /x",
            StartTime: 0L,
            DurationMs: 100L,
            Status: TraceStatus.Ok,
            SpanCount: 1,
            Services: SingleService);
        var page = new Page<TraceSummary>([traceSummary], "next-cursor", true);
        traceProvider
            .SearchAsync(Arg.Any<TraceSearchQuery>(), Arg.Any<CancellationToken>())
            .Returns(page);
        var request = new ListTracesRequest
        {
            Service = "api",
            StartUnixMs = 0L,
            EndUnixMs = 100L,
        };
        var controller = new TracesController(
            traceProvider,
            MakeService(traceProvider, logProvider),
            mapper);

        var actionResult = await controller.ListAsync(request);

        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var body = Assert.IsType<Page<TraceSummary>>(okResult.Value);
        Assert.Single(body.Items);
        Assert.Equal("next-cursor", body.Cursor);
        Assert.True(body.HasMore);
        await traceProvider.Received(1).SearchAsync(
            Arg.Is<TraceSearchQuery>(static q =>
                q.Service == "api" &&
                q.StartUnixMs == 0L &&
                q.EndUnixMs == 100L),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    ///     <c>GetAsync</c> happy path — service returns Full view, mapper
    ///     projects to response, controller returns 200 OK.
    /// </summary>
    [Fact]
    public async Task GetAsync_TraceFound_ReturnsOkWithCorrelatedLogs()
    {
        var traceProvider = Substitute.For<ITraceProvider>();
        var logProvider = Substitute.For<ILogProvider>();
        var mapper = Substitute.For<ITracesMapper>();
        var trace = MakeTrace();
        traceProvider
            .GetByIdAsync(AnyTraceId, Arg.Any<CancellationToken>())
            .Returns(trace);
        logProvider
            .ListByTraceAsync(AnyTraceId, Arg.Any<TimeRange>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<LogEntry>>([]));
        var expected = new GetTraceResponse
        {
            Trace = trace,
            CorrelatedLogs = [],
            Mode = RequestViewMode.SpansOnly,
            Markers = [],
        };
        mapper.ToResponse(Arg.Any<RequestView>()).Returns(expected);
        var controller = new TracesController(
            traceProvider,
            MakeService(traceProvider, logProvider),
            mapper);

        var actionResult = await controller.GetAsync(AnyTraceId.Value);

        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Same(expected, okResult.Value);
        await logProvider.Received(1).ListByTraceAsync(
            AnyTraceId,
            Arg.Any<TimeRange>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    ///     Logs-only: no spans, logs present → 200 (not 404).
    /// </summary>
    [Fact]
    public async Task GetAsync_LogsOnly_ReturnsOkNot404()
    {
        var traceProvider = Substitute.For<ITraceProvider>();
        var logProvider = Substitute.For<ILogProvider>();
        var mapper = Substitute.For<ITracesMapper>();
        var log = new LogEntry(
            Timestamp: 1L,
            Level: LogLevel.Info,
            Service: "api",
            TraceId: AnyTraceId,
            SpanId: null,
            Message: "only-log",
            Fields: new Dictionary<string, string>());
        IReadOnlyList<LogEntry> logs = [log];
        traceProvider
            .GetByIdAsync(AnyTraceId, Arg.Any<CancellationToken>())
            .Returns((TraceDetail?)null);
        logProvider
            .ListByTraceAsync(AnyTraceId, Arg.Any<TimeRange>(), Arg.Any<CancellationToken>())
            .Returns(logs);
        var expected = new GetTraceResponse
        {
            Trace = null,
            CorrelatedLogs = logs,
            Mode = RequestViewMode.LogsOnly,
            Markers = [],
        };
        mapper.ToResponse(Arg.Any<RequestView>()).Returns(expected);
        var controller = new TracesController(
            traceProvider,
            MakeService(traceProvider, logProvider),
            mapper);

        var actionResult = await controller.GetAsync(AnyTraceId.Value);

        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Same(expected, okResult.Value);
    }

    /// <summary>
    ///     Both empty → <see cref="ProviderNotFoundException" /> with
    ///     <see cref="TracesErrors.TraceNotFound" />.
    /// </summary>
    [Fact]
    public async Task GetAsync_BothEmpty_ThrowsProviderNotFoundWithErrorCode()
    {
        var traceProvider = Substitute.For<ITraceProvider>();
        var logProvider = Substitute.For<ILogProvider>();
        var mapper = Substitute.For<ITracesMapper>();
        traceProvider
            .GetByIdAsync(AnyTraceId, Arg.Any<CancellationToken>())
            .Returns((TraceDetail?)null);
        logProvider
            .ListByTraceAsync(AnyTraceId, Arg.Any<TimeRange>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<LogEntry>>([]));
        var controller = new TracesController(
            traceProvider,
            MakeService(traceProvider, logProvider),
            mapper);

        var exception = await Assert.ThrowsAsync<ProviderNotFoundException>(
            () => controller.GetAsync(AnyTraceId.Value));

        Assert.Equal(TracesErrors.TraceNotFound, exception.Code);
        Assert.Contains(AnyTraceId.Value, exception.Message);
    }

    /// <summary>
    ///     Cancellation token reaches both provider calls.
    /// </summary>
    [Fact]
    public async Task GetAsync_ForwardsCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var traceProvider = Substitute.For<ITraceProvider>();
        var logProvider = Substitute.For<ILogProvider>();
        var mapper = Substitute.For<ITracesMapper>();
        var trace = MakeTrace();
        traceProvider.GetByIdAsync(AnyTraceId, cts.Token).Returns(trace);
        logProvider
            .ListByTraceAsync(AnyTraceId, Arg.Any<TimeRange>(), cts.Token)
            .Returns(Task.FromResult<IReadOnlyList<LogEntry>>([]));
        mapper.ToResponse(Arg.Any<RequestView>())
            .Returns(new GetTraceResponse
            {
                Trace = trace,
                CorrelatedLogs = [],
                Mode = RequestViewMode.SpansOnly,
                Markers = [],
            });
        var controller = new TracesController(
            traceProvider,
            MakeService(traceProvider, logProvider),
            mapper);

        await controller.GetAsync(AnyTraceId.Value, cts.Token);

        await traceProvider.Received(1).GetByIdAsync(AnyTraceId, cts.Token);
        await logProvider.Received(1).ListByTraceAsync(
            AnyTraceId, Arg.Any<TimeRange>(), cts.Token);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return now;
        }
    }
}
