using NSubstitute;
using Tessera.Modules.Traces.Contracts.Errors;
using Tessera.Modules.Traces.Errors;
using Tessera.Modules.Traces.Services;
using Tessera.Shared.Kernel.Domain.Resources;
using Tessera.Shared.Kernel.Domain.Spans;
using Tessera.Shared.Kernel.Domain.Traces;
using Tessera.Shared.Kernel.Exceptions;
using Tessera.Shared.Kernel.Identifiers;
using Tessera.Shared.Kernel.Observability;
using Tessera.Shared.Kernel.Pagination;
using Tessera.Shared.Kernel.Providers.Traces;

namespace Tessera.Modules.Traces.Unit.Services;

/// <summary>
///     <see cref="ErrorsInboxService" /> — search → filter Error → cap detail
///     fetch → group by exception key.
/// </summary>
public sealed class ErrorsInboxServiceTests
{
    private static readonly TraceId TraceOne = new("11111111111111111111111111111111");
    private static readonly TraceId TraceTwo = new("22222222222222222222222222222222");
    private static readonly SpanId SpanA = new("aaaaaaaaaaaaaaaa");

    private static readonly Resource EmptyResource = new(
        ServiceName: "api",
        ServiceNamespace: null,
        DeploymentEnvironment: null,
        Attributes: new Dictionary<string, string>());

    private static readonly string[] SingleService = ["api"];

    private static ListErrorsRequest ValidRequest(int? limit = 50)
    {
        return new ListErrorsRequest
        {
            StartUnixMs = 1_000L,
            EndUnixMs = 2_000L,
            Service = "api",
            Limit = limit,
        };
    }

    private static TraceSummary Summary(TraceId id, TraceStatus status)
    {
        return new TraceSummary(
            id,
            RootService: "api",
            RootOperation: "GET /x",
            StartTime: 1_000L,
            DurationMs: 50L,
            Status: status,
            SpanCount: 1,
            Services: SingleService);
    }

    private static TraceDetail DetailWithException(TraceId id, string message)
    {
        var attributes = new Dictionary<string, string>
        {
            [SemanticConventions.ExceptionType] = "System.Exception",
            [SemanticConventions.ExceptionMessage] = message,
        };
        var span = new Span(
            SpanA,
            ParentSpanId: null,
            Service: "api",
            Operation: "GET /x",
            StartTime: 1_000L,
            DurationMs: 50L,
            Status: TraceStatus.Error,
            Kind: SpanKind.Server,
            Resource: EmptyResource,
            Tags: new Dictionary<string, string>(),
            Events: [new SpanEvent(1_010L, "exception", attributes)]);

        return new TraceDetail(
            id,
            RootService: "api",
            RootOperation: "GET /x",
            StartTime: 1_000L,
            DurationMs: 50L,
            Status: TraceStatus.Error,
            Spans: [span]);
    }

    /// <inheritdoc/>
    [Fact]
    public async Task ListAsync_TwoTracesSameException_GroupsWithCountTwo()
    {
        var provider = Substitute.For<ITraceProvider>();
        var page = new Page<TraceSummary>(
            [Summary(TraceOne, TraceStatus.Error), Summary(TraceTwo, TraceStatus.Error)],
            Cursor: null,
            HasMore: false);
        provider.SearchAsync(Arg.Any<TraceSearchQuery>(), Arg.Any<CancellationToken>()).Returns(page);
        provider.GetByIdAsync(TraceOne, Arg.Any<CancellationToken>())
            .Returns(DetailWithException(TraceOne, "order 42 failed"));
        provider.GetByIdAsync(TraceTwo, Arg.Any<CancellationToken>())
            .Returns(DetailWithException(TraceTwo, "order 99 failed"));
        var service = new ErrorsInboxService(provider);

        var groups = await service.ListAsync(ValidRequest());

        Assert.Single(groups);
        Assert.Equal(2, groups[0].Count);
        Assert.Equal("System.Exception", groups[0].ExceptionType);
        Assert.Equal("System.Exception|order {n} failed", groups[0].Key);
        Assert.Equal(2, groups[0].SampleTraceIds.Count);
        Assert.Contains(TraceOne.Value, groups[0].SampleTraceIds);
        Assert.Contains(TraceTwo.Value, groups[0].SampleTraceIds);
    }

    /// <inheritdoc/>
    [Fact]
    public async Task ListAsync_EmptySearch_ReturnsEmpty()
    {
        var provider = Substitute.For<ITraceProvider>();
        provider.SearchAsync(Arg.Any<TraceSearchQuery>(), Arg.Any<CancellationToken>())
            .Returns(new Page<TraceSummary>([], Cursor: null, HasMore: false));
        var service = new ErrorsInboxService(provider);

        var groups = await service.ListAsync(ValidRequest());

        Assert.Empty(groups);
        await provider.DidNotReceive().GetByIdAsync(Arg.Any<TraceId>(), Arg.Any<CancellationToken>());
    }

    /// <inheritdoc/>
    [Fact]
    public async Task ListAsync_OnlyOkSummaries_SkipsDetailFetch()
    {
        var provider = Substitute.For<ITraceProvider>();
        provider.SearchAsync(Arg.Any<TraceSearchQuery>(), Arg.Any<CancellationToken>())
            .Returns(new Page<TraceSummary>(
                [Summary(TraceOne, TraceStatus.Ok)],
                Cursor: null,
                HasMore: false));
        var service = new ErrorsInboxService(provider);

        var groups = await service.ListAsync(ValidRequest());

        Assert.Empty(groups);
        await provider.DidNotReceive().GetByIdAsync(Arg.Any<TraceId>(), Arg.Any<CancellationToken>());
    }

    /// <inheritdoc/>
    [Fact]
    public async Task ListAsync_CapsDetailFetchAtMaxDetailFetches()
    {
        var provider = Substitute.For<ITraceProvider>();
        var summaries = new List<TraceSummary>();
        for (var index = 0; index < ErrorsInboxService.MaxDetailFetches + 5; index++)
        {
            var hex = index.ToString("x32", System.Globalization.CultureInfo.InvariantCulture);
            summaries.Add(Summary(new TraceId(hex), TraceStatus.Error));
        }

        provider.SearchAsync(Arg.Any<TraceSearchQuery>(), Arg.Any<CancellationToken>())
            .Returns(new Page<TraceSummary>(summaries, Cursor: null, HasMore: false));
        provider.GetByIdAsync(Arg.Any<TraceId>(), Arg.Any<CancellationToken>())
            .Returns(static callInfo => DetailWithException(callInfo.Arg<TraceId>(), "boom"));
        var service = new ErrorsInboxService(provider);

        await service.ListAsync(ValidRequest(limit: 100));

        await provider.Received(ErrorsInboxService.MaxDetailFetches)
            .GetByIdAsync(Arg.Any<TraceId>(), Arg.Any<CancellationToken>());
    }

    /// <inheritdoc/>
    [Fact]
    public async Task ListAsync_MissingRange_ThrowsSearchInvalid()
    {
        var provider = Substitute.For<ITraceProvider>();
        var service = new ErrorsInboxService(provider);

        var exception = await Assert.ThrowsAsync<ProviderException>(
            () => service.ListAsync(new ListErrorsRequest { StartUnixMs = 0, EndUnixMs = 0 }));

        Assert.Equal(TracesErrors.SearchInvalid, exception.Code);
        await provider.DidNotReceive().SearchAsync(Arg.Any<TraceSearchQuery>(), Arg.Any<CancellationToken>());
    }

    /// <inheritdoc/>
    [Fact]
    public async Task ListAsync_InvertedRange_ThrowsSearchInvalid()
    {
        var provider = Substitute.For<ITraceProvider>();
        var service = new ErrorsInboxService(provider);

        var exception = await Assert.ThrowsAsync<ProviderException>(
            () => service.ListAsync(new ListErrorsRequest
            {
                StartUnixMs = 2_000L,
                EndUnixMs = 1_000L,
            }));

        Assert.Equal(TracesErrors.SearchInvalid, exception.Code);
    }

    /// <inheritdoc/>
    [Fact]
    public async Task ListAsync_ForwardsServiceAndLimitToSearch()
    {
        var provider = Substitute.For<ITraceProvider>();
        provider.SearchAsync(Arg.Any<TraceSearchQuery>(), Arg.Any<CancellationToken>())
            .Returns(new Page<TraceSummary>([], Cursor: null, HasMore: false));
        var service = new ErrorsInboxService(provider);
        var request = new ListErrorsRequest
        {
            StartUnixMs = 10L,
            EndUnixMs = 20L,
            Service = "checkout",
            Limit = 12,
        };

        await service.ListAsync(request);

        await provider.Received(1).SearchAsync(
            Arg.Is<TraceSearchQuery>(static query =>
                query.Service == "checkout"
                && query.StartUnixMs == 10L
                && query.EndUnixMs == 20L
                && query.Limit == 12),
            Arg.Any<CancellationToken>());
    }
}
