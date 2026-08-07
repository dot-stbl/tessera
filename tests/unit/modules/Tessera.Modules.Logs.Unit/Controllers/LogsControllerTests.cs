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

namespace Tessera.Modules.Logs.Unit.Controllers;

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
            Level: LogLevel.Info,
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
            Arg.Is<LogQuery>(static q =>
                q.TraceId == new TraceId("0123456789abcdef0123456789abcdef") &&
                q.Stream == null &&
                q.StartUnixMs == 0L &&
                q.EndUnixMs == 100L),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    ///     When <c>traceId</c> is missing the controller throws
    ///     <see cref="ProviderException" /> with
    ///     <see cref="LogsErrors.TraceIdRequired" />. Host IExceptionHandler
    ///     maps to a 400 ProblemDetails body — MVP-01 documented behaviour per
    ///     architecture.md §Wire format. The provider is NOT called.
    /// </summary>
    [Fact]
    public async Task ListAsync_WithoutTraceId_ThrowsProviderExceptionWithRequiredCode()
    {
        var provider = Substitute.For<ILogProvider>();
        var request = new ListLogsRequest
        {
            TraceId = null,
            StartUnixMs = 0L,
            EndUnixMs = 100L,
        };
        var controller = new LogsController(provider);

        var exception = await Assert.ThrowsAsync<ProviderException>(
            () => controller.ListAsync(request));

        Assert.Equal(LogsErrors.TraceIdRequired, exception.Code);
        Assert.Contains("traceId", exception.Message);
        await provider.DidNotReceive().QueryAsync(
            Arg.Any<LogQuery>(), Arg.Any<CancellationToken>());
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
            .Returns(new Page<LogEntry>([], null, false));
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
}
