using Tessera.Shared.Kernel.Analysis.Deps;
using Tessera.Shared.Kernel.Analysis.Deps.Models;
using Tessera.Shared.Kernel.Domain.Resources;
using Tessera.Shared.Kernel.Domain.Spans;
using Tessera.Shared.Kernel.Domain.Traces;
using Tessera.Shared.Kernel.Identifiers;
using Tessera.Shared.Kernel.Observability;
using Xunit;

namespace Tessera.Shared.Unit.Analysis;

/// <summary>
///     Pure per-trace dependency graph tests for <see cref="DependencyAnalysis" />.
/// </summary>
public sealed class DependencyAnalysisTests
{
    private static readonly TraceId AnyTraceId = new("0123456789abcdef0123456789abcdef");
    private static readonly SpanId RootId = new("1111111111111111");
    private static readonly SpanId ClientId = new("aaaaaaaaaaaaaaaa");
    private static readonly SpanId ServerId = new("bbbbbbbbbbbbbbbb");
    private static readonly SpanId Client2Id = new("cccccccccccccccc");

    private static Resource MakeResource(string serviceName)
    {
        return new Resource(
            ServiceName: serviceName,
            ServiceNamespace: null,
            DeploymentEnvironment: null,
            Attributes: new Dictionary<string, string>());
    }

    private static Span MakeSpan(
        SpanId id,
        string service,
        SpanKind kind,
        SpanId? parentId = null,
        TraceStatus status = TraceStatus.Ok,
        IReadOnlyDictionary<string, string>? tags = null,
        string operation = "op")
    {
        return new Span(
            id,
            ParentSpanId: parentId,
            Service: service,
            Operation: operation,
            StartTime: 1_000L,
            DurationMs: 50L,
            Status: status,
            Kind: kind,
            Resource: MakeResource(service),
            Tags: tags ?? new Dictionary<string, string>(),
            Events: []);
    }

    private static TraceDetail MakeTrace(params Span[] spans)
    {
        return new TraceDetail(
            AnyTraceId,
            RootService: spans.Length > 0 ? spans[0].Service : "api",
            RootOperation: "root",
            StartTime: 1_000L,
            DurationMs: 100L,
            Status: TraceStatus.Ok,
            Spans: spans);
    }

    /// <inheritdoc/>
    [Fact]
    public void FromTrace_ClientParentOfServer_DifferentService_CreatesEdge()
    {
        var root = MakeSpan(RootId, "checkout-api", SpanKind.Server);
        var client = MakeSpan(ClientId, "checkout-api", SpanKind.Client, parentId: RootId);
        var server = MakeSpan(ServerId, "currency", SpanKind.Server, parentId: ClientId);
        var graph = DependencyAnalysis.FromTrace(MakeTrace(root, client, server));

        Assert.Equal(2, graph.Nodes.Count);
        Assert.Contains(graph.Nodes, static node => node is { Id: "checkout-api", Kind: DependencyNodeKind.Service });
        Assert.Contains(graph.Nodes, static node => node is { Id: "currency", Kind: DependencyNodeKind.Service });
        Assert.Single(graph.Edges);
        Assert.Equal("checkout-api", graph.Edges[0].FromId);
        Assert.Equal("currency", graph.Edges[0].ToId);
        Assert.Equal(1, graph.Edges[0].CallCount);
        Assert.Equal(0, graph.Edges[0].ErrorCount);
    }

    /// <inheritdoc/>
    [Fact]
    public void FromTrace_ClientWithDbSystem_NoServerChild_SyntheticDatabase()
    {
        var tags = new Dictionary<string, string>
        {
            [SemanticConventions.DbSystem] = "postgresql",
            ["db.name"] = "orders",
        };
        var client = MakeSpan(ClientId, "checkout-api", SpanKind.Client, tags: tags);
        var graph = DependencyAnalysis.FromTrace(MakeTrace(client));

        Assert.Contains(graph.Nodes, static node => node is
        {
            Id: "db:postgresql:orders",
            Name: "orders",
            Kind: DependencyNodeKind.Database,
        });
        Assert.Single(graph.Edges);
        Assert.Equal("checkout-api", graph.Edges[0].FromId);
        Assert.Equal("db:postgresql:orders", graph.Edges[0].ToId);
    }

    /// <inheritdoc/>
    [Fact]
    public void FromTrace_ClientWithDbSystemOnly_IdIsDbSystem()
    {
        var tags = new Dictionary<string, string>
        {
            [SemanticConventions.DbSystem] = "postgresql",
        };
        var client = MakeSpan(ClientId, "checkout-api", SpanKind.Client, tags: tags);
        var graph = DependencyAnalysis.FromTrace(MakeTrace(client));

        Assert.Contains(graph.Nodes, static node => node.Id == "db:postgresql" && node.Name == "postgresql");
    }

    /// <inheritdoc/>
    [Fact]
    public void FromTrace_ClientWithPeerService_NoServerChild_SyntheticExternal()
    {
        var tags = new Dictionary<string, string>
        {
            [SemanticConventions.PeerService] = "stripe",
        };
        var client = MakeSpan(
            ClientId,
            "checkout-api",
            SpanKind.Client,
            status: TraceStatus.Error,
            tags: tags);
        var graph = DependencyAnalysis.FromTrace(MakeTrace(client));

        Assert.Contains(graph.Nodes, static node => node is
        {
            Id: "external:stripe",
            Name: "stripe",
            Kind: DependencyNodeKind.External,
        });
        Assert.Single(graph.Edges);
        Assert.Equal("external:stripe", graph.Edges[0].ToId);
        Assert.Equal(1, graph.Edges[0].CallCount);
        Assert.Equal(1, graph.Edges[0].ErrorCount);
    }

    /// <inheritdoc/>
    [Fact]
    public void FromTrace_ClientWithServerAddress_SyntheticExternal()
    {
        var tags = new Dictionary<string, string>
        {
            [SemanticConventions.ServerAddress] = "api.partner.example",
        };
        var client = MakeSpan(ClientId, "checkout-api", SpanKind.Client, tags: tags);
        var graph = DependencyAnalysis.FromTrace(MakeTrace(client));

        Assert.Contains(graph.Nodes, static node => node.Id == "external:api.partner.example");
        Assert.Equal("checkout-api", graph.Edges[0].FromId);
    }

    /// <inheritdoc/>
    [Fact]
    public void FromTrace_ServerChildWinsOverDbTags()
    {
        var tags = new Dictionary<string, string>
        {
            [SemanticConventions.DbSystem] = "postgresql",
            [SemanticConventions.PeerService] = "stripe",
        };
        var client = MakeSpan(ClientId, "checkout-api", SpanKind.Client, tags: tags);
        var server = MakeSpan(ServerId, "currency", SpanKind.Server, parentId: ClientId);
        var graph = DependencyAnalysis.FromTrace(MakeTrace(client, server));

        Assert.DoesNotContain(graph.Nodes, static node => node.Kind is DependencyNodeKind.Database);
        Assert.DoesNotContain(graph.Nodes, static node => node.Kind is DependencyNodeKind.External);
        Assert.Single(graph.Edges);
        Assert.Equal("currency", graph.Edges[0].ToId);
    }

    /// <inheritdoc/>
    [Fact]
    public void FromTrace_AggregatesCallAndErrorCounts()
    {
        var tags = new Dictionary<string, string>
        {
            [SemanticConventions.PeerService] = "stripe",
        };
        var ok = MakeSpan(ClientId, "checkout-api", SpanKind.Client, tags: tags);
        var err = MakeSpan(
            Client2Id,
            "checkout-api",
            SpanKind.Client,
            status: TraceStatus.Error,
            tags: tags);
        var graph = DependencyAnalysis.FromTrace(MakeTrace(ok, err));

        Assert.Single(graph.Edges);
        Assert.Equal(2, graph.Edges[0].CallCount);
        Assert.Equal(1, graph.Edges[0].ErrorCount);
    }

    /// <inheritdoc/>
    [Fact]
    public void FromTrace_SameServiceServerChild_Skipped()
    {
        var client = MakeSpan(ClientId, "checkout-api", SpanKind.Client);
        var sameService = MakeSpan(ServerId, "checkout-api", SpanKind.Server, parentId: ClientId);
        var graph = DependencyAnalysis.FromTrace(MakeTrace(client, sameService));

        Assert.Empty(graph.Edges);
    }

    /// <inheritdoc/>
    [Fact]
    public void FromTrace_NullOrEmpty_ReturnsEmptyGraph()
    {
        var empty = DependencyAnalysis.FromTrace(null);
        Assert.Empty(empty.Nodes);
        Assert.Empty(empty.Edges);

        var noSpans = DependencyAnalysis.FromTrace(MakeTrace());
        Assert.Empty(noSpans.Nodes);
        Assert.Empty(noSpans.Edges);
    }
}
