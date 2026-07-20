using Microsoft.Extensions.Logging;
using Tessera.Providers.Victoria.Clients;
using Tessera.Shared.Kernel.Domain.Services;

namespace Tessera.Providers.Victoria.Implementation.Mapping;

/// <summary>
///     Pure mappings + non-throwing I/O wrappers for the discovery provider.
///     All helpers are <c>internal static</c> so they're top-level (not
///     private to a class) and unit-testable in isolation.
/// </summary>
internal static class VictoriaDiscoveryMapper
{
    private const string StreamFieldMarker = "\"_stream\":\"";

    /// <summary>
    ///     Extract the <c>_stream</c> field value from a single NDJSON line.
    ///     Returns null if the line doesn't contain the field or is malformed.
    /// </summary>
    public static string? ExtractStreamField(string ndjsonLine)
    {
        var idx = ndjsonLine.IndexOf(StreamFieldMarker, StringComparison.Ordinal);
        if (idx < 0)
        {
            return null;
        }

        var start = idx + StreamFieldMarker.Length;
        var end = ndjsonLine.IndexOf('"', start);
        return end < 0 ? null : ndjsonLine[start..end];
    }

    /// <summary>
    ///     Walk NDJSON body lines, extract each line's <c>_stream</c> field,
    ///     deduplicate while preserving insertion order, return distinct streams.
    /// </summary>
    public static IReadOnlyList<string> ExtractDistinctStreams(string ndjsonBody)
    {
        var streams = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var line in ndjsonBody.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var stream = ExtractStreamField(line);
            if (!string.IsNullOrEmpty(stream) && seen.Add(stream))
            {
                streams.Add(stream);
            }
        }

        return streams;
    }

    /// <summary>
    ///     Fetch the service inventory from VT <c>/services</c>, swallowing
    ///     network failures and returning an empty list. Per-provider failure
    ///     must not cascade to a UI crash.
    /// </summary>
    public static async Task<IReadOnlyList<string>> TryListTraceServicesAsync(
        IVictoriaTracesClient client,
        ILogger logger,
        string tenant,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await client.GetServicesAsync(tenant, cancellationToken);
            return response.Data;
        }
        catch (HttpRequestException ex)
        {
            // exceptions.md §2: catch only the type you can act on. The
            // catch-all swallow is gone — a non-network failure (e.g.
            // mapping) is now a hard error surfaced through the caller's
            // exception chain rather than a silent empty result.
            logger.LogWarning(
                ex,
                "Victoria traces /services unreachable; returning empty list for tenant {Tenant}",
                tenant);
            return [];
        }
    }

    /// <summary>
    ///     Fetch distinct <c>_stream</c> values from VL LogsQL, swallowing
    ///     network failures and returning an empty list. Per-provider failure
    ///     must not cascade to a UI crash.
    /// </summary>
    public static async Task<IReadOnlyList<string>> TryListLogStreamsAsync(
        IVictoriaLogsClient client,
        ILogger logger,
        string tenant,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await client.QueryAsync(tenant, "*", 1000, null, null, cancellationToken);
            return !response.IsSuccessStatusCode
                ? []
                : ExtractDistinctStreams(await response.Content.ReadAsStringAsync(cancellationToken));
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(
                ex,
                "Victoria logs /select/* unreachable; returning empty list for tenant {Tenant}",
                tenant);
            return [];
        }
    }

    /// <summary>
    ///     Build a deduplicated, alphabetically ordered list of
    ///     <see cref="ServiceSummary" /> from trace service + log stream names.
    ///     MVP-01 placeholder: span/operation counts are zero, no operations
    ///     are listed (will be backed by an aggregates query in MVP-02).
    /// </summary>
    public static IReadOnlyList<ServiceSummary> ToServiceList(
        IReadOnlyList<string> traceServices,
        IReadOnlyList<string> logStreams)
    {
        var unique = new SortedSet<string>(traceServices.Concat(logStreams), StringComparer.Ordinal);
        var services = new List<ServiceSummary>(unique.Count);

        foreach (var name in unique)
        {
            services.Add(new ServiceSummary(
                name,
                SpanCount: 0,
                ErrorCount: 0,
                Operations: Array.Empty<Tessera.Shared.Kernel.Domain.Services.ServiceOperation>()));
        }

        return services;
    }
}
