using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Tessera.Modules.Traces.Contracts;
using Tessera.Modules.Traces.Controllers;
using Tessera.Modules.Traces.Errors;
using Tessera.Modules.Traces.Mapping;
using Tessera.Shared.Kernel.Domain.Logs;
using Tessera.Shared.Kernel.Domain.Traces;
using Tessera.Shared.Kernel.Exceptions;
using Tessera.Shared.Kernel.Identifiers;
using Tessera.Shared.Kernel.Pagination;
using Tessera.Shared.Kernel.Providers.Logs;
using Tessera.Shared.Kernel.Providers.Traces;
using Tessera.Shared.Kernel.Time;

namespace Tessera.Modules.Traces.Unit.Controllers;

/// <summary>
///     <see cref="TracesController" /> orchestration tests — exercise the
///     List/Search endpoint, the Trace-detail-with-correlated-logs endpoint,
///     the 404 surface path, and cancellation propagation. Uses
///     <see cref="ITraceProvider" /> + <see cref="ILogProvider" /> +
///     <see cref="ITracesMapper" /> NSubstitute mocks.
/// </summary>
public sealed class TracesControllerTests
{
    private static readonly TraceId AnyTraceId = new("0123456789abcdef0123456789abcdef");

    private static readonly string[] SingleService = ["api"];

    private static TraceDetail MakeTrace(TraceId? id = null)
    {
        return new TraceDetail(
            id ?? AnyTraceId,
            "api-gateway",
            "GET /orders",
            StartTime: 0L,
            DurationMs: 100L,
            Status: TraceStatus.Ok,
            Spans: []);
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
        var controller = new TracesController(traceProvider, logProvider, mapper);

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
    ///     <c>GetAsync</c> happy path — provider returns a non-null trace, log
    ///     provider returns correlated logs, mapper projects both into the
    ///     <c>GetTraceResponse</c> body. Returns 200 OK.
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
        };
        mapper.ToResponse(trace, Arg.Any<IReadOnlyList<LogEntry>>()).Returns(expected);
        var controller = new TracesController(traceProvider, logProvider, mapper);

        var actionResult = await controller.GetAsync(AnyTraceId.Value);

        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Same(expected, okResult.Value);
        await logProvider.Received(1).ListByTraceAsync(
            AnyTraceId,
            Arg.Any<TimeRange>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    ///     <c>GetAsync</c> 404 surface path — provider returns null,
    ///     controller throws <see cref="ProviderNotFoundException" /> with
    ///     <see cref="TracesErrors.TraceNotFound" /> code. The host's
    ///     <c>IExceptionHandler</c> maps the typed exception to a 404
    ///     ProblemDetails body — verified in
    ///     <c>Tessera.Host.UnitTests</c>.
    /// </summary>
    [Fact]
    public async Task GetAsync_TraceNotFound_ThrowsProviderNotFoundWithErrorCode()
    {
        var traceProvider = Substitute.For<ITraceProvider>();
        var logProvider = Substitute.For<ILogProvider>();
        var mapper = Substitute.For<ITracesMapper>();
        traceProvider
            .GetByIdAsync(AnyTraceId, Arg.Any<CancellationToken>())
            .Returns((TraceDetail?)null);
        var controller = new TracesController(traceProvider, logProvider, mapper);

        var exception = await Assert.ThrowsAsync<ProviderNotFoundException>(
            () => controller.GetAsync(AnyTraceId.Value));

        Assert.Equal(TracesErrors.TraceNotFound, exception.Code);
        Assert.Contains(AnyTraceId.Value, exception.Message);
        await logProvider.DidNotReceive().ListByTraceAsync(
            Arg.Any<TraceId>(), Arg.Any<TimeRange>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    ///     <c>GetAsync</c> propagates the caller-supplied cancellation token
    ///     to both provider calls without modification — exercised here as a
    ///     single call to keep the assertion obvious; the
    ///     <c>ToLogCorrelationRange</c> helper builds the range from the
    ///     trace window so the test just verifies the token reached the
    ///     underlying provider.
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
        mapper.ToResponse(trace, Arg.Any<IReadOnlyList<LogEntry>>())
            .Returns(new GetTraceResponse { Trace = trace, CorrelatedLogs = [] });
        var controller = new TracesController(traceProvider, logProvider, mapper);

        await controller.GetAsync(AnyTraceId.Value, cts.Token);

        await traceProvider.Received(1).GetByIdAsync(AnyTraceId, cts.Token);
        await logProvider.Received(1).ListByTraceAsync(
            AnyTraceId, Arg.Any<TimeRange>(), cts.Token);
    }
}
