using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Tessera.Modules.Logs.Contracts;
using Tessera.Modules.Logs.Controllers;
using Tessera.Modules.Logs.Errors;
using Tessera.Shared.Kernel.Domain.Logs;
using Tessera.Shared.Kernel.Exceptions;
using Tessera.Shared.Kernel.Identifiers;
using Tessera.Shared.Kernel.Pagination;
using Tessera.Shared.Kernel.Providers.Logs;
using Tessera.Shared.Kernel.Time;
using Xunit;

namespace Tessera.Modules.Logs.Tests.Controllers;

/// <summary>
///     <see cref="LogsController" /> orchestration tests — MVP-01 only
///     supports trace-correlation lookup; the missing-<see cref="TraceId" />
///     branch is the only meaningful validation surface in this scope.
/// </summary>
public sealed class LogsControllerTests
{
    /// <summary>
    ///     When <c>traceId</c> is supplied the request projects into a
    ///     non-null <see cref="LogQuery" />, the provider is called, and
    ///     the resulting page is returned as 200 OK.
    /// </summary>
    [Fact]
    public async Task ListAsync_WithTraceId_ReturnsProviderPageAsOk()
    {
        var provider = Substitute.For<ILogProvider>();
        var logEntry = new LogEntry(
            Timestamp: 1L,
            Level: LogLevel.Information,
            Service: "api-gateway",
            TraceId: null,
            SpanId: null,
            Message: "order placed",
            Fields: new Dictionary<string, string>());
        var page = new Page<LogEntry>([logEntry], null, false);
        provider.QueryAsync(Arg.Any<LogQuery>(), Arg.Any<CancellationToken>()).Returns(page);
        var request = new ListLogsRequest
        {
            TraceId = "0123456789abcdef0123456789abcdef",
            StartUnixMs = 0L,
            EndUnixMs = 100L,
        };
        var controller = new LogsController(provider);

        var actionResult = await controller.ListAsync(request);

        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var body = Assert.IsType<Page<LogEntry>>(okResult.Value);
        Assert.Single(body.Items);
        await provider.Received(1).QueryAsync(
            Arg.Is<LogQuery>(q =>
                q.TraceId == new TraceId("0123456789abcdef0123456789abcdef") &&
                q.Stream == null &&
                q.StartUnixMs == 0L &&
                q.EndUnixMs == 100L),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    ///     MVP-01 documented behaviour: <see cref="LogsController" /> requires
    ///     <c>traceId</c>. The current implementation forwards the request to
    ///     <see cref="ILogProvider" /> regardless of whether
    ///     <see cref="Tessera.Modules.Logs.Contracts.ListLogsRequest.TraceId" />
    ///     is supplied — the controller doesn't validate it. The MVP-02 path
    ///     will be to make <see cref="Tessera.Modules.Logs.Contracts.ListLogsRequest.ToLogQuery" />
    ///     return <c>null</c> when TraceId is missing, which the controller
    ///     already guards against with an <c>is not { } query</c> check.
    ///     This test pins the MVP-01 behaviour: controller delegates the
    ///     decision to the provider, which has to enforce it (or 200 OK with
    ///     every log in the time range for the wrong request).
    /// </summary>
    [Fact]
    public async Task ListAsync_WithoutTraceId_ForwardsToProviderForBackendDecision()
    {
        var provider = Substitute.For<ILogProvider>();
        provider.QueryAsync(Arg.Any<LogQuery>(), Arg.Any<CancellationToken>())
            .Returns(new Page<LogEntry>(Array.Empty<LogEntry>(), null, false));
        var request = new ListLogsRequest
        {
            TraceId = null,
            StartUnixMs = 0L,
            EndUnixMs = 100L,
        };
        var controller = new LogsController(provider);

        var actionResult = await controller.ListAsync(request);

        Assert.IsType<OkObjectResult>(actionResult.Result);
        await provider.Received(1).QueryAsync(
            Arg.Is<LogQuery>(q => q.TraceId == null && q.StartUnixMs == 0L && q.EndUnixMs == 100L),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    ///     The cancellation token flows to <see cref="ILogProvider.QueryAsync" />
    ///     unmodified — the controller neither replaces it with
    ///     <c>HttpContext.RequestAborted</c> nor splits it into a sub-range.
    /// </summary>
    [Fact]
    public async Task ListAsync_ForwardsCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var provider = Substitute.For<ILogProvider>();
        provider.QueryAsync(Arg.Any<LogQuery>(), cts.Token)
            .Returns(new Page<LogEntry>(Array.Empty<LogEntry>(), null, false));
        var request = new ListLogsRequest
        {
            TraceId = "0123456789abcdef0123456789abcdef",
            StartUnixMs = 0L,
            EndUnixMs = 100L,
        };
        var controller = new LogsController(provider);

        await controller.ListAsync(request, cts.Token);

        await provider.Received(1).QueryAsync(Arg.Any<LogQuery>(), cts.Token);
    }

    private static readonly string[] NoServices = [];
}
