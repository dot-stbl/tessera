using Microsoft.Extensions.Logging;
using Tessera.Providers.Victoria.Clients;
using Tessera.Shared.Kernel.Domain.Services;

namespace Tessera.Providers.Victoria.Implementation.Mapping;

/// <summary>
///     Pure mappings + non-throwing I/O wrappers for the discovery provider.
/// </summary>
internal static class VictoriaDiscoveryMapper
{
    private const string StreamFieldMarker = "\"_stream\":\"";

    /// <summary>Extract the <c>_stream</c> field value from a single NDJSON line.</summary>
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

    /// <summary>Walk NDJSON body lines and return distinct <c>_stream</c> values.</summary>
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
    ///     Fetch the service inventory from VT <c>/services</c>, swallowing network failures.
    /// </summary>
    public static async Task<IReadOnlyList<string>> TryListTraceServicesAsync(
        IVictoriaTracesClient client,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await client.GetServicesAsync(cancellationToken);
            return response.Data;
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Victoria traces /services unreachable; returning empty list");
            return [];
        }
    }

    /// <summary>
    ///     Fetch distinct <c>_stream</c> values from VL LogsQL, swallowing network failures.
    /// </summary>
    public static async Task<IReadOnlyList<string>> TryListLogStreamsAsync(
        IVictoriaLogsClient client,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await client.QueryAsync("*", 1000, null, null, cancellationToken);
            return !response.IsSuccessStatusCode
                ? []
                : ExtractDistinctStreams(await response.Content.ReadAsStringAsync(cancellationToken));
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Victoria logs /select/* unreachable; returning empty list");
            return [];
        }
    }

    /// <summary>
    ///     Build a deduplicated, alphabetically ordered list of
    ///     <see cref="ServiceSummary" /> from trace service + log stream names.
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
                Operations: []));
        }

        return services;
    }
}
