using System.Text.Json;
using Tessera.Providers.Victoria.Dto.VictoriaLogs;
using Tessera.Shared.Kernel.Domain.Logs;
using Tessera.Shared.Kernel.Identifiers;
using Tessera.Shared.Kernel.Observability;
using Tessera.Shared.Kernel.Pagination;
using Tessera.Shared.Kernel.Providers.Logs;
using Tessera.Shared.Kernel.Time;

namespace Tessera.Providers.Victoria.Implementation.Mapping;

/// <summary>
///     File-static singleton for the <see cref="JsonSerializerOptions" /> the
///     VictoriaLogs NDJSON deserializer uses. Extracted from
///     <see cref="VictoriaLogMapper" /> per anti-patterns.md §6 — JSON
///     options are config, not per-class state, and must live in a
///     dedicated <c>*JsonOptions</c> holder so the naming policy is
///     shared across the provider without static-initialiser ordering
///     hazards.
/// </summary>
internal static class VictoriaLogJsonOptions
{
    /// <summary>
    ///     The single options instance the provider's NDJSON parser uses.
    ///     <c>static readonly</c> is intentional — the converter graph
    ///     is built once on first access and cached for the process
    ///     lifetime per <c>System.Text.Json</c>'s lazy caching.
    /// </summary>
    public static readonly JsonSerializerOptions Instance = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };
}

/// <summary>
///     Pure mappings for VictoriaLogs NDJSON + LogsQL query construction.
///     Held as <c>internal static</c> so they're testable in isolation,
///     not private to the provider class.
/// </summary>
/// <remarks>
///     Trace correlation field: query uses <c>_trace_id</c> (VictoriaLogs
///     stream field convention for OTel). Parse accepts both <c>_trace_id</c>
///     and <c>trace_id</c> so producers that emit either form still correlate.
///     Verify against live VL before treating empty correlation as a product bug.
/// </remarks>
internal static class VictoriaLogMapper
{
    /// <summary>
    ///     Build a LogsQL filter string from a <see cref="LogQuery" />.
    ///     Returns "*" when no filter or trace id is supplied.
    ///     Trace filter uses <c>_trace_id</c> (VL OTel stream field).
    /// </summary>
    public static string BuildLogsQuery(LogQuery query)
    {
        if (query.TraceId is not null)
        {
            return $@"_trace_id:""{query.TraceId.Value}""";
        }

        if (!string.IsNullOrWhiteSpace(query.Filter))
        {
            return query.Filter;
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
                dto = JsonSerializer.Deserialize<VLLogEntry>(line, VictoriaLogJsonOptions.Instance);
            }
            catch (JsonException)
            {
                continue;
            }

            if (dto is null)
            {
                continue;
            }

            var traceId = ReadTraceId(dto.Fields) ?? traceFilter;
            var spanId = ReadSpanId(dto.Fields);
            var level = ResolveLogLevel(dto.Fields);
            var service = ResolveService(dto);

            entries.Add(new LogEntry(
                dto.Time.ToUnixTimeMilliseconds(),
                level,
                service,
                traceId,
                spanId,
                dto.Msg,
                dto.Fields));
        }

        return entries;
    }

    /// <summary>
    ///     Resolve log level from OTel <c>severity_number</c> (1–24) with
    ///     text fallback on <c>level</c> / <c>severity_text</c>.
    /// </summary>
    public static LogLevel ResolveLogLevel(IReadOnlyDictionary<string, string> fields)
    {
        if (fields.TryGetValue("severity_number", out var numberText)
            && int.TryParse(numberText, out var severityNumber))
        {
            return FromSeverityNumber(severityNumber);
        }

        if (fields.TryGetValue("severity_text", out var severityText))
        {
            return ToLogLevel(severityText);
        }

        if (fields.TryGetValue("level", out var levelText))
        {
            return ToLogLevel(levelText);
        }

        return LogLevel.Info;
    }

    /// <summary>
    ///     Map OTel severity_number (1–24) to kernel <see cref="LogLevel" />.
    /// </summary>
    public static LogLevel FromSeverityNumber(int severityNumber)
    {
        return severityNumber switch
        {
            >= 1 and <= 4 => LogLevel.Trace,
            >= 5 and <= 8 => LogLevel.Debug,
            >= 9 and <= 12 => LogLevel.Info,
            >= 13 and <= 16 => LogLevel.Warn,
            >= 17 and <= 20 => LogLevel.Error,
            >= 21 and <= 24 => LogLevel.Fatal,
            _ => LogLevel.Info,
        };
    }

    /// <summary>
    ///     Convert a LogsQL level string to the kernel's <see cref="LogLevel" />.
    ///     Unknown / missing values map to <see cref="LogLevel.Info" />.
    /// </summary>
    public static LogLevel ToLogLevel(string? value)
    {
        if (value is null)
        {
            return LogLevel.Info;
        }

        return value.ToUpperInvariant() switch
        {
            "TRACE" => LogLevel.Trace,
            "DEBUG" => LogLevel.Debug,
            "INFO" or "INFORMATION" => LogLevel.Info,
            "WARN" or "WARNING" => LogLevel.Warn,
            "ERROR" => LogLevel.Error,
            "FATAL" or "CRITICAL" => LogLevel.Fatal,
            _ => LogLevel.Info,
        };
    }

    /// <summary>
    ///     Service identity from resource <c>service.name</c>, then common
    ///     field aliases, then <c>_stream</c> as last resort.
    /// </summary>
    public static string ResolveService(VLLogEntry dto)
    {
        if (dto.Fields.TryGetValue(SemanticConventions.ServiceName, out var fromSemconv)
            && !string.IsNullOrWhiteSpace(fromSemconv))
        {
            return fromSemconv;
        }

        if (dto.Fields.TryGetValue("service", out var fromService)
            && !string.IsNullOrWhiteSpace(fromService))
        {
            return fromService;
        }

        return string.IsNullOrWhiteSpace(dto.Stream) ? "unknown" : dto.Stream;
    }

    /// <summary>
    ///     Prefer <c>_trace_id</c> (VL stream field), then <c>trace_id</c>.
    /// </summary>
    public static TraceId? ReadTraceId(IReadOnlyDictionary<string, string> fields)
    {
        if (fields.TryGetValue("_trace_id", out var underscored))
        {
            return ToTraceId(underscored);
        }

        if (fields.TryGetValue("trace_id", out var plain))
        {
            return ToTraceId(plain);
        }

        return null;
    }

    /// <summary>
    ///     Prefer <c>_span_id</c>, then <c>span_id</c>.
    /// </summary>
    public static SpanId? ReadSpanId(IReadOnlyDictionary<string, string> fields)
    {
        if (fields.TryGetValue("_span_id", out var underscored))
        {
            return ToSpanId(underscored);
        }

        if (fields.TryGetValue("span_id", out var plain))
        {
            return ToSpanId(plain);
        }

        return null;
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
