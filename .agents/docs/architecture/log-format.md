# Log format

> **Decision (locked 2026-07-19):** `dot.case` at export, PascalCase in source.

Tessera uses OpenTelemetry log SDK with custom `DotCaseLogRecordProcessor`
to enforce **dot.case** for all log attribute keys at export. Code may use
PascalCase placeholders (idiomatic .NET) — transformation happens in
`Tessera.Shared.Telemetry.LogFormat.DotCaseLogRecordProcessor`.

This makes tessera logs portable across OTLP-aware tools (Jaeger, Tempo,
VictoriaLogs) and consistent with OTel semantic conventions
(`http.method`, `http.status_code` are already dot.case).

## Source code convention

```csharp
// ✅ CORRECT — PascalCase placeholders, transformer handles conversion
logger.LogInformation(
    "Trace {TraceId} fetched in {DurationMs}ms with {SpanCount} spans",
    traceId, durationMs, spanCount);
// On wire: { "trace.id": "...", "duration.ms": ..., "span.count": ... }

// ❌ WRONG — manually writing dot.case placeholders (won't compile)
logger.LogInformation("Trace {trace.id} fetched", traceId);

// ❌ WRONG — snake_case in source (violates naming-and-types.md)
logger.LogInformation("Trace {trace_id} in {duration_ms}ms", traceId, durationMs);
```

## Reserved keys (OTel log contract)

These keys are reserved and MUST NOT be used as property names in log
statements — they're owned by the OTel SDK:

| Key | Meaning |
|-----|---------|
| `time` | timestamp |
| `severity` | log level (TRACE/DEBUG/INFO/WARN/ERROR/FATAL) |
| `body` | message |
| `trace_id` | OTel trace context |
| `span_id` | OTel span context |
| `trace_flags` | OTel trace flags |
| `event_name` | EventId.Name |
| `exception.type` | exception type name |
| `exception.message` | exception message |
| `exception.stacktrace` | stack trace |

If you accidentally use `{Time}` thinking custom time, the transformer
preserves the reserved key — your log collides with the contract. **Log
warning will fire at processor registration** for known collisions.

## EventId naming

```csharp
// ✅ CORRECT — PascalCase in code, transformer converts
logger.LogInformation(
    new EventId(1001, "TraceFetched"),
    "Trace {TraceId} fetched",
    traceId);
// On wire: { "event.name": "trace.fetched", "trace.id": ... }

// ❌ WRONG — dot.case in code (bypasses convention)
logger.LogInformation(
    new EventId(1001, "trace.fetched"),
    "...");
```

## Nested objects — recursive transformation

```csharp
public sealed record User(string Name, string Email);

logger.LogInformation("User {User} authenticated", new User("Alice", "a@x.com"));
// On wire: { "user": { "name": "Alice", "email": "a@x.com" } }
```

## Custom spans + structured logs

```csharp
public sealed class GetTraceHandler(
    IVictoriaTracesClient client,
    ILogger<GetTraceHandler> logger)
{
    private static readonly ActivitySource Activity = new("Tessera.Traces");

    public async Task<TraceDetail> HandleAsync(string traceId, CancellationToken ct)
    {
        using var activity = Activity.StartActivity("traces.get");
        activity?.SetTag("trace.id", traceId);            // dot.case already

        var response = await client.GetTraceAsync(traceId, ct);
        var tree = ReconstructSpanTree(response);

        activity?.SetTag("vt.spans.count", tree.Spans.Count);
        activity?.SetTag("vt.duration_ms", tree.DurationMs);

        logger.LogInformation(
            "Trace {TraceId} fetched: {SpanCount} spans, {DurationMs}ms duration",
            traceId, tree.Spans.Count, tree.DurationMs);

        return tree;
    }
}
```

## Implementation

`src/shared/Tessera.Shared.Telemetry/LogFormat/DotCaseLogRecordProcessor.cs`:

```csharp
namespace Tessera.Shared.Telemetry.LogFormat;

public sealed class DotCaseLogRecordProcessor : BaseProcessor<LogRecord>
{
    private static readonly char[] Separators = ['_', '-', '.'];

    public override void OnEnd(LogRecord log)
    {
        if (log.Attributes is null || log.Attributes.Count == 0) return;

        var transformed = new List<KeyValuePair<string, object?>>(log.Attributes.Count);
        foreach (var (key, value) in log.Attributes)
        {
            transformed.Add(new(ToDotCase(key), TransformValue(value)));
        }
        log.Attributes = transformed;
    }

    private static object? TransformValue(object? value) => value switch
    {
        IReadOnlyList<KeyValuePair<string, object?>> nested =>
            nested.Select(kv => new KeyValuePair<string, object?>(
                ToDotCase(kv.Key), TransformValue(kv.Value))).ToList(),
        _ => value
    };

    public static string ToDotCase(string key)
    {
        if (string.IsNullOrEmpty(key)) return key;
        var sb = new StringBuilder(key.Length + 4);
        for (var i = 0; i < key.Length; i++)
        {
            var c = key[i];
            if (Array.IndexOf(Separators, c) >= 0)
            {
                if (sb.Length > 0 && sb[^1] != '.') sb.Append('.');
            }
            else if (char.IsUpper(c))
            {
                if (sb.Length > 0 && sb[^1] != '.' && !char.IsUpper(sb[^1]))
                    sb.Append('.');
                sb.Append(char.ToLowerInvariant(c));
            }
            else sb.Append(c);
        }
        return sb.ToString().Trim('.');
    }
}
```

## Registration

```csharp
// src/host/Tessera.Host/Program.cs
builder.Logging.AddOpenTelemetry(opts =>
{
    opts.AddProcessor<DotCaseLogRecordProcessor>();
    opts.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
});
```

## Performance budget

- Process < 5µs per attribute
- Zero allocations in steady state (reuse `StringBuilder`, no LINQ in hot path)
- Profile with realistic log volume (10k/sec) before adding new features

## Test requirements

- Unit snapshot tests in `tests/unit/core/Tessera.Shared.Unit/`:
  - `DotCaseLogRecordProcessorTests.ToDotCase_PascalCase` → `"trace.id"`
  - `DotCaseLogRecordProcessorTests.ToDotCase_SnakeCase` → `"trace.id"`
  - `DotCaseLogRecordProcessorTests.ToDotCase_CamelCase` → `"trace.id"`
  - `DotCaseLogRecordProcessorTests.ToDotCase_AlreadyDot` → `"trace.id"` (no-op)
  - `DotCaseLogRecordProcessorTests.OnEnd_TransformsAttributes`
  - `DotCaseLogRecordProcessorTests.OnEnd_PreservesReservedKeys`
  - `DotCaseLogRecordProcessorTests.OnEnd_RecursesNestedObjects`
- Property-based tests: random strings → always valid dot.case output
- Integration: end-to-end log → OTLP → VictoriaLogs → query `trace.id:"..."` → found

## Related docs

- `architecture.md` — observability architecture
- `victoria-stack.md` — VL query language (uses dot.case field names)
- `../rules/coding/naming-and-types.md` — C# naming convention