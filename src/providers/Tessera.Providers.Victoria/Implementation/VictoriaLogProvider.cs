using System.Text.Json;
using Tessera.Providers.Victoria.Clients;
using Tessera.Providers.Victoria.Configuration;
using Tessera.Providers.Victoria.Dto.VictoriaLogs;
using Tessera.Shared.Kernel.Domain.Logs;
using Tessera.Shared.Kernel.Identifiers;
using Tessera.Shared.Kernel.Pagination;
using Tessera.Shared.Kernel.Providers.Logs;
using Tessera.Shared.Kernel.Time;

namespace Tessera.Providers.Victoria.Implementation;

/// <summary>
///     VictoriaLogs implementation of <see cref="ILogProvider" />.
///     Maps LogsQL NDJSON responses to domain <see cref="LogEntry" /> records.
/// </summary>
public sealed class VictoriaLogProvider(IVictoriaLogsClient client, VictoriaOptions options) : ILogProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    /// <inheritdoc />
    public async Task<Page<LogEntry>> QueryAsync(LogQuery query, CancellationToken ct)
    {
        var logsql = BuildLogsQuery(query);
        using var response = await client.QueryAsync(
            options.Tenant, logsql, query.Limit,
            query.StartUnixMs is null ? null : ToIso8601(query.StartUnixMs.Value),
            query.EndUnixMs is null ? null : ToIso8601(query.EndUnixMs.Value),
            ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(ct);
        var entries = ParseNdjson(body, query.TraceId);
        return new Page<LogEntry>(entries, Cursor: null, HasMore: false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LogEntry>> ListByTraceAsync(
        TraceId traceId,
        TimeRange range,
        CancellationToken ct)
    {
        var query = new LogQuery(
            TraceId: traceId,
            Stream: null,
            StartUnixMs: range.StartUnixMs,
            EndUnixMs: range.EndUnixMs,
            Filter: null,
            Limit: 500);
        var page = await QueryAsync(query, ct);
        return page.Items;
    }

    private static string BuildLogsQuery(LogQuery query)
    {
        if (query.TraceId is not null)
        {
            return $@"_trace_id:""{query.TraceId.Value}""";
        }

        if (!string.IsNullOrWhiteSpace(query.Filter))
        {
            return query.Filter!;
        }

        return "*";
    }

    private static List<LogEntry> ParseNdjson(string body, TraceId? traceFilter)
    {
        var entries = new List<LogEntry>();
        foreach (var line in body.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            VLLogEntry? dto;
            try
            {
                dto = JsonSerializer.Deserialize<VLLogEntry>(line, JsonOptions);
            }
            catch (JsonException)
            {
                continue;
            }

            if (dto is null)
            {
                continue;
            }

            var traceId = dto.Fields.TryGetValue("trace_id", out var tid)
                ? TryParseTraceId(tid)
                : traceFilter;
            var spanId = dto.Fields.TryGetValue("span_id", out var sid)
                ? TryParseSpanId(sid)
                : null;
            var level = ParseLevel(dto.Fields.GetValueOrDefault("level"));

            entries.Add(new LogEntry(
                Timestamp: dto.Time.ToUnixTimeMilliseconds(),
                Level: level,
                Service: dto.Stream,
                TraceId: traceId,
                SpanId: spanId,
                Message: dto.Msg,
                Fields: dto.Fields));
        }

        return entries;
    }

    private static LogLevel ParseLevel(string? value)
    {
        return value switch
        {
            "TRACE" => LogLevel.Trace,
            "DEBUG" => LogLevel.Debug,
            "INFO" or "INFORMATION" => LogLevel.Information,
            "WARN" or "WARNING" => LogLevel.Warning,
            "ERROR" => LogLevel.Error,
            "FATAL" or "CRITICAL" => LogLevel.Fatal,
            _ => LogLevel.Information,
        };
    }

    private static TraceId? TryParseTraceId(string value)
    {
        if (!string.IsNullOrWhiteSpace(value) && value.Length is >= 16 and <= 32)
        {
            return new TraceId(value);
        }

        return null;
    }

    private static SpanId? TryParseSpanId(string value)
    {
        if (!string.IsNullOrWhiteSpace(value) && value.Length is >= 8 and <= 16)
        {
            return new SpanId(value);
        }

        return null;
    }

    private static string ToIso8601(long unixMs)
    {
        return DateTimeOffset.FromUnixTimeMilliseconds(unixMs).UtcDateTime.ToString("o");
    }
}