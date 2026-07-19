
using Tessera.Providers.Victoria.Clients;
using Tessera.Providers.Victoria.Configuration;
using Tessera.Shared.Kernel.Domain.Services;
using Tessera.Shared.Kernel.Providers.Discovery;

namespace Tessera.Providers.Victoria.Implementation;
/// <summary>
///     VictoriaTraces + VictoriaLogs implementation of <see cref="IDiscoveryProvider" />.
///     Merges service inventory from VT <c>/services</c> and VL
///     <c>_stream</c> fields, returning deduplicated <see cref="ServiceSummary" /> records.
/// </summary>
public sealed class VictoriaDiscoveryProvider(
    IVictoriaTracesClient tracesClient,
    IVictoriaLogsClient logsClient,
    VictoriaOptions options) : IDiscoveryProvider
{

    /// <inheritdoc />
    public async Task<IReadOnlyList<ServiceSummary>> ListServicesAsync(CancellationToken ct)
    {
        var traceServices = await SafeListTraceServicesAsync(ct);
        var logStreams = await SafeListLogStreamsAsync(ct);

        var services = new HashSet<string>(
            traceServices.Concat(logStreams),
            StringComparer.Ordinal);

        return services
            .OrderBy(s => s, StringComparer.Ordinal)
            .Select(name => new ServiceSummary(
                Name: name,
                SpanCount: 0,
                ErrorCount: 0,
                Operations: Array.Empty<ServiceOperation>()))
            .ToList();
    }

    private async Task<IReadOnlyList<string>> SafeListTraceServicesAsync(CancellationToken ct)
    {
        try
        {
            var response = await tracesClient.GetServicesAsync(options.Tenant, ct);
            return response.Data;
        }
        catch
        {
            return [];
        }
    }

    private async Task<IReadOnlyList<string>> SafeListLogStreamsAsync(CancellationToken ct)
    {
        try
        {
            using var response = await logsClient.QueryAsync(
                options.Tenant, "*", 1000, null, null, ct);
            if (!response.IsSuccessStatusCode)
            {
                return [];
            }
            var body = await response.Content.ReadAsStringAsync(ct);
            var streams = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var line in body.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var stream = ExtractStreamField(line);
                if (!string.IsNullOrEmpty(stream) && seen.Add(stream))
                {
                    streams.Add(stream);
                }
            }

            return streams;
        }
        catch
        {
            return [];
        }
    }

    private static string? ExtractStreamField(string ndjsonLine)
    {
        const string marker = "\"_stream\":\"";
        var idx = ndjsonLine.IndexOf(marker, StringComparison.Ordinal);
        if (idx < 0)
        {
            return null;
        }

        var start = idx + marker.Length;
        var end = ndjsonLine.IndexOf('"', start);
        if (end < 0)
        {
            return null;
        }

        return ndjsonLine[start..end];
    }
}
