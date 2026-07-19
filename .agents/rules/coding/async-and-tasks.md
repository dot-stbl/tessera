---
description: c# async/await — Async suffix required, ConfigureAwait banned, CancellationToken propagation, ValueTask usage
globs: ["**/*.cs"]
always: true
---

# Async and tasks

## 1. `Async` suffix — REQUIRED

All methods returning `Task`, `Task<T>`, `ValueTask`, `ValueTask<T>` MUST
end with `Async`. Including trivial pass-through.

```csharp
// ✅ Correct
public async Task<TraceDetail> GetTraceAsync(string traceId, CancellationToken ct);
public Task<bool> IsEnabledAsync();

// ❌ Wrong
public async Task<TraceDetail> GetTrace(string traceId);
public Task<bool> IsEnabled();
```

**Enforcement:** `VSTHRD200` (severity=error).

## 2. `ConfigureAwait(false)` — BANNED in app code

```csharp
// ❌ Wrong — legacy pattern, no benefit in .NET 8+
await repository.GetUserAsync(id, cancellationToken).ConfigureAwait(false);

// ✅ Correct — modern async/await, no overhead
await repository.GetUserAsync(id, cancellationToken);
```

**Enforcement:** `CA2007` (severity=error).

Exception: **library code that runs in older runtimes** may need it. Tessera
targets .NET 10 only — no exception.

## 3. `CancellationToken` propagation

```csharp
public sealed class GetTraceHandler(IVictoriaTracesClient client)
{
    public async Task<TraceDetail> HandleAsync(string traceId, CancellationToken ct)
    {
        // ✅ Pass through to all async calls
        var response = await client.GetTraceAsync(traceId, ct);

        // ✅ Propagate to nested calls
        var logs = await LoadLogsAsync(traceId, ct);
        return new TraceDetail(response, logs);
    }

    private async Task<List<LogEntry>> LoadLogsAsync(string traceId, CancellationToken ct)
    {
        // Don't create new CT — use the one passed in
        return await logsClient.ListByTraceAsync(traceId, ct);
    }
}
```

**Default value:** `CancellationToken.None` is acceptable ONLY for public
APIs that don't take CT (rare).

## 4. `Task` vs `ValueTask`

```csharp
// ✅ Task<T> — IO, HTTP, DB (always async)
public async Task<TraceDetail> GetTraceAsync(string traceId, CancellationToken ct);

// ✅ ValueTask<T> — when sync path is possible (cache hit, pre-computed)
public ValueTask<TraceDetail?> GetCachedTraceAsync(string traceId);

// ❌ Wrong — ValueTask where always async
public ValueTask<TraceDetail> FetchRemoteTraceAsync(string traceId);
```

**Rule:** Default to `Task<T>`. Use `ValueTask<T>` only when measured benefit.

## 5. `async void` — BANNED

```csharp
// ❌ FORBIDDEN — unobservable exceptions, can't be awaited
public async void Button_Click(object sender, EventArgs e)
{
    await DoWorkAsync();  // if this throws, app crashes
}

// ✅ Correct — return Task
public async Task OnClickAsync(CancellationToken ct)
{
    await DoWorkAsync(ct);
}
```

**Exception:** event handlers in UI frameworks (WinForms, WPF) where the
signature is fixed. Tessera backend has no such case.

**Enforcement:** `VSTHRD100`, `VSTHRD101`, `CA2012`.

## 6. No `.Result` / `.Wait()`

```csharp
// ❌ Wrong — sync over async, deadlocks
var trace = client.GetTraceAsync(id, ct).Result;
client.GetTraceAsync(id, ct).Wait();

// ✅ Correct — async all the way
var trace = await client.GetTraceAsync(id, ct);
```

**Exception:** test code where you want to verify an exception synchronously
(use `Assert.ThrowsAsync` instead).

## 7. Parallel async calls — `Task.WhenAll`

```csharp
// ✅ Parallel — fetch traces and logs concurrently
var tracesTask = client.SearchTracesAsync(query, ct);
var logsTask = client.QueryLogsAsync(query, ct);
await Task.WhenAll(tracesTask, logsTask);
var traces = await tracesTask;
var logs = await logsTask;

// ❌ Sequential when not needed
var traces = await client.SearchTracesAsync(query, ct);
var logs = await client.QueryLogsAsync(query, ct);
```

Use `Task.WhenAll` when calls are independent.

## 8. `Parallel.ForEachAsync` for bounded parallelism

```csharp
// ✅ Bounded parallel fetch
await Parallel.ForEachAsync(
    traceIds,
    new ParallelOptions { MaxDegreeOfParallelism = 10, CancellationToken = ct },
    async (id, ctInner) =>
    {
        var trace = await client.GetTraceAsync(id, ctInner);
        cache.Set(id, trace);
    });
```

## 9. Channels for async pipelines

```csharp
public sealed class TraceStreamer(IVictoriaTracesClient client)
{
    public async IAsyncEnumerable<TraceDetail> StreamAsync(
        IAsyncEnumerable<string> traceIds,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var id in traceIds.WithCancellation(ct))
        {
            yield return await client.GetTraceAsync(id, ct);
        }
    }
}
```

## Self-audit grep

```bash
# async void (banned)
rg -n "async\s+void\b" src/ --type cs

# ConfigureAwait (banned)
rg -n "ConfigureAwait" src/ --type cs

# .Result / .Wait() (sync over async)
rg -n "\.(Result|Wait)\(\)" src/ --type cs

# Missing Async suffix on Task-returning methods
rg -nB1 "Task<" src/ --type cs | rg "^\s*public\b" | rg -v "Async"
```

## Related rules

- `naming-and-types.md` — naming, sealed
- `code-shape.md` — var, braces
- `di-lifetimes.md` — scoped vs transient for async-safe services