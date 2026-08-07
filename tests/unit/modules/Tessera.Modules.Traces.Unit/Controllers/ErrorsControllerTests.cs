using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Tessera.Modules.Traces.Contracts.Errors;
using Tessera.Modules.Traces.Controllers;
using Tessera.Modules.Traces.Services;
using Tessera.Shared.Kernel.Domain.Traces;
using Tessera.Shared.Kernel.Pagination;
using Tessera.Shared.Kernel.Providers.Traces;

namespace Tessera.Modules.Traces.Unit.Controllers;

/// <summary>
///     <see cref="ErrorsController" /> smoke — delegates to
///     <see cref="ErrorsInboxService" /> and returns 200 with groups.
/// </summary>
public sealed class ErrorsControllerTests
{
    /// <inheritdoc/>
    [Fact]
    public async Task ListAsync_ReturnsOkWithGroups()
    {
        var provider = Substitute.For<ITraceProvider>();
        provider.SearchAsync(Arg.Any<TraceSearchQuery>(), Arg.Any<CancellationToken>())
            .Returns(new Page<TraceSummary>([], Cursor: null, HasMore: false));
        var service = new ErrorsInboxService(provider);
        var controller = new ErrorsController(service);
        var request = new ListErrorsRequest
        {
            StartUnixMs = 1L,
            EndUnixMs = 2L,
        };

        var actionResult = await controller.ListAsync(request);

        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var body = Assert.IsType<IReadOnlyList<ErrorGroupSummary>>(okResult.Value, exactMatch: false);
        Assert.Empty(body);
    }
}
