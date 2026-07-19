namespace Tessera.Providers.Victoria.Dto.VictoriaLogs;

/// <summary>
///     Single VictoriaLogs log entry (NDJSON row).
///     Standard fields: <c>_msg</c> (message body), <c>_stream</c> (stream id),
///     <c>_time</c> (RFC3339 timestamp). Remaining properties are dynamic
///     (<c>level</c>, <c>trace_id</c>, <c>span_id</c>, app-specific tags) and
///     surface as <see cref="Fields" />.
/// </summary>
public sealed record VLLogEntry(
    string Msg,
    string Stream,
    DateTimeOffset Time,
    IReadOnlyDictionary<string, string> Fields);
