# Per-rule grep catalog (project-local extension to worker-audit.md)

> Scope: the regex patterns below are the executable form of the
> rules they map to. The agent runs them as part of any audit
> self-gate, not as a one-off lookup. The companion rule
> `.agents/rules/process/audit-debug.md` describes the protocol
> when a missed violation is flagged.

> Adding a new entry here means: "this rule family is now part of
> the default scan — the agent will surface violations even when
> the user does not name the rule".

## Catalog

| § | Rule | Grep / regex | What it catches |
|---|---|---|---|
| code-shape §1 | Merge `var + if is null` / `var + if is not null` into `is { } x` | `var\s+\w+\s*=[^;]+;\s*\r?\n\s*if\s*\(\s*\1\s+is\s+(null\|not\s+null)\s*\)` (multi-line, backref) | `TomlConfigurationProvider.cs:36`, `TracesController.cs:73` |
| code-shape §9 | No `private` methods in production classes (override / explicit-helper-allowed-list only) | `^\s*private\s+(?:static\s+)?(?:async\s+)?(?:Task\|Task<\|ValueTask\|void\|bool\|int\|long\|string\|object\|[A-Z]\w+)\s+[A-Z]\w*\s*\(` outside `tests/` and outside `IDisposable.Dispose(bool)` etc. | sweep all `src/` |
| code-shape §11 | No `ThrowIf*` argument checks under `<Nullable>enable</Nullable>` | `ArgumentNullException\.ThrowIfNull\b\|ArgumentException\.ThrowIfNullOrEmpty\b\|ArgumentException\.ThrowIfNullOrWhiteSpace\b` | sweep all `src/` |
| code-shape §5 | Method bodies use `{ }` block — no `=>` expression-body | `public\s+(?:static\s+)?(?:async\s+)?(?:\w+(?:<[^>]+>)?\s+[A-Z]\w+\s*\(.*\)\s*=>)` (one-line method bodies) | sweep all `src/` |
| error-mapping §5 | Outgoing HTTP boundary translates to `ProviderException` | `EnsureSuccessStatusCode\b` and `(?:RestService\.For<\|client\.(?:GetAsync\|PostAsync\|PutAsync\|DeleteAsync\|SendAsync))` outside `try { }` | `VictoriaLogProvider.cs:32` |
| exceptions §2 | No catch-all (`catch (Exception)`, `catch { }`, `catch (...)`) — but `Assert.ThrowsAsync<T>(...)` and `Assert.Throws<T>(...)` are xunit assertion method calls, not catch statements | `catch\s*(?:\(\s*Exception\b\|\{\s*\})` (with manual lookbehind) | `VictoriaDiscoveryMapper.cs`, `Generator.cs` |
| exceptions §5 | No `throw ex;` (stack reset) | `throw\s+\w+\s*;` outside the throwing expression itself | sweep all `src/` |
| anti-patterns §6 | No inline `new JsonSerializerOptions` / no `private static readonly JsonSerializerOptions` | `private\s+(?:static\s+)?readonly\s+JsonSerializerOptions\b\|=\s*new\s+JsonSerializerOptions\s*\(` | `VictoriaLogMapper.cs:18-21` |
| di-options §5/§6 | `Bind(...).ValidateOnStart()` requires `ValidateDataAnnotations()` between them | `AddOptions<[^>]+>[^)]*\.Bind\s*\([^)]+\)\.ValidateOnStart\(\)` (single-line) — check that `.ValidateDataAnnotations()` appears between `.Bind(` and `.ValidateOnStart()` | `VictoriaServiceCollectionExtensions.cs:24-26` |
| http-resilience-refit §2 | No bare `new HttpClient()` outside DI helpers and tests | `new HttpClient\s*\{` outside `tests/`, `RefitExtensions.cs`, `*DependencyInjection*.cs` | `HttpClientFactory.cs:17` |
| time-and-wire-format §3 | No direct `DateTime(Offset)?.UtcNow` in production code outside the resolver | `\bDateTime(?:Offset)?\.(?:UtcNow\|Now)\b` outside `tests/`, `TesseraConfigPaths.cs`, `TesseraDataPaths.cs` | `TimeRange.cs:34`, `Generator.cs:112-120` |
| async-and-tasks §6 | No `ConfigureAwait(false)` in app code | `\.ConfigureAwait\(false\)` | sweep all `src/` |
| api-design §2 | Controllers return `ActionResult<T>` / `IActionResult` (no `Task<T>` direct) | `public\s+(?:async\s+)?Task<[A-Z]\w+>\s+[A-Z]\w+\s*\([^)]*\)\s*\{?\s*$` in `*Controller.cs` outside `private static async Task` | sweep `*Controller.cs` |
| api-design §4 | No `ModelState.AddModelError` / `ValidationProblem(ModelState)` | `ModelState\.AddModelError\b\|ValidationProblem\(ModelState\)` | sweep all `src/` |
| naming-and-types §1 | No banned parameter names: `ct`, `req`, `resp`, `err`, `msg`, `svc`, `u`, `x`, `tmp` (as `Type ct, ...`) | `\b(?:CancellationToken\|string\|int\|long\|bool\|Stream)\s+(?:ct\|req\|resp\|err\|msg\|svc\|u\|x\|tmp)\b` | sweep `tests/Tessera.LoadGen/Scenarios/*.cs` |

## How to run

```bash
# cross-platform PowerShell scan (output: file:line per hit)
Get-ChildItem -Recurse -Filter "*.cs" src/ tests/ | Where-Object { $_.FullName -notmatch "bin\\|obj\\" } | ForEach-Object { ... }
```

Run during `worker-audit.md §2` self-gate after every non-trivial
edit. Treat any hit as a violation — if it isn't, add a `# ok`
comment to the line so future scans can ignore it via context.