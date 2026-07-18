---
description: structured logging conventions — ILogger<T>, structured properties, sensitive data masking, log levels
globs: ["**/*.cs"]
always: true
---

# Logging conventions

Tessera uses `Microsoft.Extensions.Logging` with OpenTelemetry exporter.
See `../../docs/architecture/log-format.md` for the dot.case transform.

## 1. Inject `ILogger<T>`, not static logger

```csharp
// ✅ Correct — DI'd logger
public sealed class GetTraceHandler(
    IVictoriaTracesClient client,
    ILogger<GetTraceHandler> logger)
{
    public async Task<TraceDetail> HandleAsync(string traceId, CancellationToken ct)
    {
        logger.LogInformation("Fetching trace {TraceId}", traceId);
        // ...
    }
}

// ❌ Wrong — static logger
private static readonly ILogger Log = LogManager.GetCurrentClassLogger();
```

Static logger loses category info (which class) and bypasses DI mocking.

## 2. Structured properties, not concatenation

```csharp
// ✅ Correct — structured properties
logger.LogInformation(
    "Trace {TraceId} fetched: {SpanCount} spans, {DurationMs}ms duration",
    traceId, spanCount, durationMs);

// ❌ Wrong — concatenated message
logger.LogInformation(
    $"Trace {traceId} fetched: {spanCount} spans, {durationMs}ms duration");
```

Structured properties enable search/aggregation. Concatenated strings don't.

## 3. Log levels — when to use what

| Level | When | Examples |
|-------|------|----------|
| `Trace` | Verbose debugging | "Entering method X with arg Y" |
| `Debug` | Diagnostic info | "Cache hit for key X" |
| `Information` | Normal operations | "Trace fetched", "Dashboard saved" |
| `Warning` | Recoverable issues | "Victoria returned 503, retrying", "Slow query > 5s" |
| `Error` | Failed operations | "Failed to fetch trace", exception details |
| `Critical` | App-level failure | "Cannot start — config invalid" |

**Default:** Information. Override per namespace in appsettings:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  }
}
```

## 4. Exception logging — log + throw

```csharp
// ✅ Correct — log with exception details, then rethrow
catch (HttpRequestException ex)
{
    logger.LogError(ex,
        "Failed to fetch trace {TraceId} from {Endpoint}",
        traceId, endpoint);
    throw;
}

// ❌ Wrong — log without exception (loses stack trace)
catch (HttpRequestException ex)
{
    logger.LogError("Failed to fetch trace: {Message}", ex.Message);
    throw;
}

// ❌ Wrong — swallow exception
catch (HttpRequestException)
{
    // silent failure
}
```

## 5. Sensitive data masking

```csharp
// ❌ Wrong — logging token
logger.LogInformation("Connecting to Victoria with token {Token}", token);

// ✅ Correct — mask sensitive values
logger.LogInformation("Connecting to Victoria with token {TokenPrefix}...",
    token[..4] + "***");

// ✅ Better — don't log tokens at all
logger.LogInformation("Connecting to Victoria (token from {Source})",
    tokenSource);
```

**Categories that must never be logged:**
- Tokens (bearer, basic auth)
- Passwords
- API keys
- PII (emails, phone, addresses) — unless explicit purpose
- Full request/response bodies containing user data

## 6. Correlation with traces

Every log statement automatically includes `trace_id` and `span_id` from the
active OTel context (via `OpenTelemetry.Extensions.Logging`).

```csharp
logger.LogInformation("Fetching trace {TraceId}", traceId);
// On wire:
// {
//   "time": "...",
//   "severity": "INFO",
//   "body": "Fetching trace abc123",
//   "trace_id": "...",
//   "span_id": "...",
//   "attributes": { "trace.id": "abc123" }
// }
```

No manual `LogContext.PushProperty` needed.

## 7. Performance — logging cost

```csharp
// ❌ Wrong — expensive operation in log args (always evaluated)
logger.LogDebug("Computed value: {Value}", ComputeExpensiveThing());

// ✅ Correct — use IsEnabled check or message template
if (logger.IsEnabled(LogLevel.Debug))
{
    logger.LogDebug("Computed value: {Value}", ComputeExpensiveThing());
}
```

Log args are still evaluated even if level is disabled. Guard expensive
computations with `IsEnabled`.

## 8. EventId — use for filtering

```csharp
logger.LogInformation(
    new EventId(1001, "TraceFetched"),
    "Trace {TraceId} fetched",
    traceId);
```

Stable EventIds per category (1000s = trace ops, 2000s = log ops, etc.)
enable downstream filtering.

## Self-audit grep

```bash
# Concatenated log messages
rg -n 'Log\w+\(".*\${' src/ --type cs

# Logged secrets (token, password, secret)
rg -ni "\.(LogDebug|LogInfo|LogWarn|LogError)\([^)]*\b(token|password|secret)\b" src/ --type cs

# Swallowed exceptions (catch without rethrow or log)
rg -nB1 "catch \(" src/ --type cs | rg -A1 "^[^/]*\bcatch\b" | rg -v "throw|Log"
```

## Related rules

- `../../docs/architecture/log-format.md` — dot.case convention
- `code-shape.md` — var, braces
- `naming-and-types.md` — sealed, naming