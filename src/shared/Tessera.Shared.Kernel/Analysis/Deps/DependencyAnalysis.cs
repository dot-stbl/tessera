using Tessera.Shared.Kernel.Analysis.Deps.Models;
using Tessera.Shared.Kernel.Domain.Spans;
using Tessera.Shared.Kernel.Domain.Traces;
using Tessera.Shared.Kernel.Identifiers;
using Tessera.Shared.Kernel.Observability;

namespace Tessera.Shared.Kernel.Analysis.Deps;

/// <summary>
///     Pure per-trace dependency graph derivation from CLIENT spans
///     (core-design §5). No I/O.
/// </summary>
/// <remarks>
///     <para>
///     Rules (first match per CLIENT span):
///     </para>
///     <list type="number">
///     <item>
///     Child SERVER span(s) with a different <c>service.name</c>
///     → edge client service → each distinct server service.
///     </item>
///     <item>
///     Else if tags contain <c>db.system</c> → synthetic Database node + edge.
///     </item>
///     <item>
///     Else if <c>peer.service</c> or <c>server.address</c> → synthetic External node + edge.
///     </item>
///     </list>
///     <para>
///     <see cref="DependencyEdge.ErrorCount" /> increments when the CLIENT span
///     has <see cref="TraceStatus.Error" /> (child SERVER status is ignored).
///     PRODUCER/CONSUMER and span links are out of scope for P9.
///     </para>
/// </remarks>
public static class DependencyAnalysis
{
    /// <summary>
    ///     Build a dependency graph for <paramref name="trace" />.
    ///     Returns an empty graph when the trace is null or has no spans.
    /// </summary>
    public static DependencyGraph FromTrace(TraceDetail? trace)
    {
        if (trace is not { Spans.Count: > 0 })
        {
            return new DependencyGraph([], []);
        }

        var childrenByParent = DependencyGraphBuild.IndexChildren(trace.Spans);
        var nodes = new Dictionary<string, DependencyNode>(StringComparer.Ordinal);
        var edgeAccumulators = new Dictionary<DependencyEdgeKey, DependencyEdgeCounts>(
            DependencyEdgeKeyComparer.Instance);

        foreach (var span in trace.Spans)
        {
            if (span.Kind is not SpanKind.Client)
            {
                continue;
            }

            var fromId = DependencyGraphBuild.ServiceNodeId(span);
            DependencyGraphBuild.EnsureServiceNode(nodes, fromId, span);
            var isError = span.Status is TraceStatus.Error;

            if (DependencyGraphBuild.TryAddServerEdges(
                    span,
                    fromId,
                    isError,
                    childrenByParent,
                    nodes,
                    edgeAccumulators))
            {
                continue;
            }

            if (DependencyGraphBuild.TryAddDatabaseEdge(
                    span,
                    fromId,
                    isError,
                    nodes,
                    edgeAccumulators))
            {
                continue;
            }

            DependencyGraphBuild.TryAddExternalEdge(
                span,
                fromId,
                isError,
                nodes,
                edgeAccumulators);
        }

        return DependencyGraphBuild.ToGraph(nodes, edgeAccumulators);
    }
}

file static class DependencyGraphBuild
{
    private const string DbNameTag = "db.name";

    public static Dictionary<SpanId, List<Span>> IndexChildren(IReadOnlyList<Span> spans)
    {
        var children = new Dictionary<SpanId, List<Span>>();
        foreach (var span in spans)
        {
            if (span.ParentSpanId is not { } parentId)
            {
                continue;
            }

            if (!children.TryGetValue(parentId, out var list))
            {
                list = [];
                children[parentId] = list;
            }

            list.Add(span);
        }

        return children;
    }

    public static string ServiceNodeId(Span span)
    {
        var name = span.Resource.ServiceName;
        if (string.IsNullOrWhiteSpace(name))
        {
            name = span.Service;
        }

        return name;
    }

    public static void EnsureServiceNode(
        Dictionary<string, DependencyNode> nodes,
        string id,
        Span span)
    {
        if (nodes.ContainsKey(id))
        {
            return;
        }

        var name = string.IsNullOrWhiteSpace(span.Resource.ServiceName)
            ? span.Service
            : span.Resource.ServiceName;
        nodes[id] = new DependencyNode(id, name, DependencyNodeKind.Service);
    }

    public static bool TryAddServerEdges(
        Span client,
        string fromId,
        bool isError,
        Dictionary<SpanId, List<Span>> childrenByParent,
        Dictionary<string, DependencyNode> nodes,
        Dictionary<DependencyEdgeKey, DependencyEdgeCounts> edgeAccumulators)
    {
        if (!childrenByParent.TryGetValue(client.SpanId, out var children))
        {
            return false;
        }

        var serverServices = new HashSet<string>(StringComparer.Ordinal);
        foreach (var child in children)
        {
            if (child.Kind is not SpanKind.Server)
            {
                continue;
            }

            var toId = ServiceNodeId(child);
            if (string.Equals(toId, fromId, StringComparison.Ordinal))
            {
                continue;
            }

            serverServices.Add(toId);
            EnsureServiceNode(nodes, toId, child);
        }

        if (serverServices.Count == 0)
        {
            return false;
        }

        foreach (var toId in serverServices)
        {
            Accumulate(edgeAccumulators, fromId, toId, isError);
        }

        return true;
    }

    public static bool TryAddDatabaseEdge(
        Span client,
        string fromId,
        bool isError,
        Dictionary<string, DependencyNode> nodes,
        Dictionary<DependencyEdgeKey, DependencyEdgeCounts> edgeAccumulators)
    {
        if (!client.Tags.TryGetValue(SemanticConventions.DbSystem, out var system)
            || string.IsNullOrWhiteSpace(system))
        {
            return false;
        }

        client.Tags.TryGetValue(DbNameTag, out var dbName);
        var id = string.IsNullOrWhiteSpace(dbName)
            ? $"db:{system}"
            : $"db:{system}:{dbName}";
        var name = string.IsNullOrWhiteSpace(dbName) ? system : dbName;

        if (!nodes.ContainsKey(id))
        {
            nodes[id] = new DependencyNode(id, name, DependencyNodeKind.Database);
        }

        Accumulate(edgeAccumulators, fromId, id, isError);
        return true;
    }

    public static bool TryAddExternalEdge(
        Span client,
        string fromId,
        bool isError,
        Dictionary<string, DependencyNode> nodes,
        Dictionary<DependencyEdgeKey, DependencyEdgeCounts> edgeAccumulators)
    {
        string? peer = null;
        if (client.Tags.TryGetValue(SemanticConventions.PeerService, out var peerService)
            && !string.IsNullOrWhiteSpace(peerService))
        {
            peer = peerService;
        }
        else if (client.Tags.TryGetValue(SemanticConventions.ServerAddress, out var address)
                 && !string.IsNullOrWhiteSpace(address))
        {
            peer = address;
        }

        if (peer is null)
        {
            return false;
        }

        var id = $"external:{peer}";
        if (!nodes.ContainsKey(id))
        {
            nodes[id] = new DependencyNode(id, peer, DependencyNodeKind.External);
        }

        Accumulate(edgeAccumulators, fromId, id, isError);
        return true;
    }

    public static void Accumulate(
        Dictionary<DependencyEdgeKey, DependencyEdgeCounts> edgeAccumulators,
        string fromId,
        string toId,
        bool isError)
    {
        var key = new DependencyEdgeKey(fromId, toId);
        if (!edgeAccumulators.TryGetValue(key, out var counts))
        {
            counts = new DependencyEdgeCounts();
            edgeAccumulators[key] = counts;
        }

        counts.CallCount++;
        if (isError)
        {
            counts.ErrorCount++;
        }
    }

    public static DependencyGraph ToGraph(
        Dictionary<string, DependencyNode> nodes,
        Dictionary<DependencyEdgeKey, DependencyEdgeCounts> edgeAccumulators)
    {
        var edges = new List<DependencyEdge>(edgeAccumulators.Count);
        foreach (var (key, counts) in edgeAccumulators)
        {
            edges.Add(new DependencyEdge(
                key.FromId,
                key.ToId,
                counts.CallCount,
                counts.ErrorCount));
        }

        return new DependencyGraph(
            nodes.Values.ToList(),
            edges);
    }
}

file sealed class DependencyEdgeCounts
{
    public int CallCount { get; set; }

    public int ErrorCount { get; set; }
}

file readonly record struct DependencyEdgeKey(string FromId, string ToId);

file sealed class DependencyEdgeKeyComparer : IEqualityComparer<DependencyEdgeKey>
{
    public static readonly DependencyEdgeKeyComparer Instance = new();

    public bool Equals(DependencyEdgeKey left, DependencyEdgeKey right)
    {
        return string.Equals(left.FromId, right.FromId, StringComparison.Ordinal)
               && string.Equals(left.ToId, right.ToId, StringComparison.Ordinal);
    }

    public int GetHashCode(DependencyEdgeKey key)
    {
        return HashCode.Combine(
            StringComparer.Ordinal.GetHashCode(key.FromId),
            StringComparer.Ordinal.GetHashCode(key.ToId));
    }
}
