using System.Text.Json;
using Tessera.Providers.Victoria.Dto.VictoriaLogs;
using Tessera.Shared.Kernel.Domain.Logs;
using Tessera.Shared.Kernel.Identifiers;
using Tessera.Shared.Kernel.Pagination;
using Tessera.Shared.Kernel.Providers.Logs;
using Tessera.Shared.Kernel.Time;

namespace Tessera.Providers.Victoria.Implementation.Mapping;

/// <summary>
///     Pure mappings for VictoriaLogs NDJSON + LogsQL query construction.
///     Held as <c>internal static</c> so they're testable in isolation,
///     not private to the provider class.
/// </summary>
internal static class VictoriaLogMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    /// <summary>
    ///     Build a LogsQL filter string from a <see cref="LogQuery" />.
    ///     Returns "*" when no filter or trace id is supplied.
    /// </summary>
    public static string BuildLogsQuery(LogQuery query)
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

    /// <summary>
    ///     Parse a LogsQL NDJSON body into a list of <see cref="LogEntry" />,
    ///     filtering by trace id when supplied.
    ///     Malformed lines are skipped silently (NDJSON ingestion is best-effort).
    /// </summary>
    public static IReadOnlyList<LogEntry> ParseNdjson(string body, TraceId? traceFilter)
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
                ? ToTraceId(tid)
                : traceFilter;

            var spanId = dto.Fields.TryGetValue("span_id", out var sid)
                ? ToSpanId(sid)
                : null;

            var level = ToLogLevel(dto.Fields.GetValueOrDefault("level"));

            entries.Add(new LogEntry(
                dto.Time.ToUnixTimeMilliseconds(),
                level,
                dto.Stream,
                traceId,
                spanId,
                dto.Msg,
                dto.Fields));
        }

        return entries;
    }

    /// <summary>
    ///     Convert a LogsQL level string to the kernel's <see cref="LogLevel" />.
    ///     Unknown / missing values map to <see cref="LogLevel.Information" />.
    /// </summary>
    public static LogLevel ToLogLevel(string? value)
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

    /// <summary>
    ///     Parse a 16-32 char trace id string into a <see cref="TraceId" />.
    ///     Returns null for malformed or empty values.
    /// </summary>
    public static TraceId? ToTraceId(string value)
    {
        return !string.IsNullOrWhiteSpace(value) && value.Length is >= 16 and <= 32
            ? new TraceId(value)
            : null;
    }

    /// <summary>
    ///     Parse an 8-16 char span id string into a <see cref="SpanId" />.
    ///     Returns null for malformed or empty values.
    /// </summary>
    public static SpanId? ToSpanId(string value)
    {
        return !string.IsNullOrWhiteSpace(value) && value.Length is >= 8 and <= 16
            ? new SpanId(value)
            : null;
    }

    /// <summary>
    ///     Convert a unix ms timestamp to ISO 8601 UTC string
    ///     for VL HTTP query string parameter.
    /// </summary>
    public static string ToIso8601(long unixMs)
    {
        return DateTimeOffset.FromUnixTimeMilliseconds(unixMs).UtcDateTime.ToString("o");
    }

    /// <summary>
    ///     Wrap a list of <see cref="LogEntry" /> as a single-page
    ///     <see cref="Page{T}" /> (logs are not paginated by cursor
    ///     in MVP-01 — the limit is a hard cap).
    /// </summary>
    public static Page<LogEntry> AsPage(IReadOnlyList<LogEntry> items)
    {
        return new Page<LogEntry>(items, Cursor: null, HasMore: false);
    }
}