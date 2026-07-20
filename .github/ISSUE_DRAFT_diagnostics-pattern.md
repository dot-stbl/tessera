# Diagnostics pattern for per-module telemetry (IClassDiagnostics vs static)

> **Status:** Draft. The architecture debate settled on
> static-class + `ActivitySource` + `Meter` + `ActivityListener` for
> Tessera MVP-02. Capturing here so the choice is documented,
> the escape-clause path to `IXxxDiagnostics` is preserved for a
> later phase (when a real CQRS/worker pipeline justifies the
> overhead), and the resumption checklist stays self-contained.

## Background

Once a Tessera module starts emitting its own telemetry (Phase 7a
landed `Tessera.Shared.Telemetry`), each module needs to decide how
to expose its diagnostics surface to the host pipeline:

```csharp
// Tessera.Modules.Preferences/Persistence/Diagnostics/UserPreferenceDiagnostics.cs
public static class UserPreferenceDiagnostics { /* ActivitySource + Counter + Histogram */ }
```

vs.

```csharp
public interface IUserPreferenceDiagnostics { IDisposable? StartUpsert(...); void RecordUpsert(...); }
```

The global rule `observability/diagnostics.md` §1 is explicit:

> "Keep it proportional. A stateless proxy needs an ActivitySource + a few counters — **not** a per-module `IXxxDiagnostics` registry with a telemetry behavior pipeline. Add that weight only if there's a CQRS/worker pipeline that justifies it."

## Two approaches

### Approach A — static-class diagnostics (current Tessera plan)

```csharp
public static class UserPreferenceDiagnostics
{
    public const string ModuleName = "Tessera.Preferences";
    private static readonly ActivitySource Activity = new(ModuleName);
    private static readonly Meter Meter = new(ModuleName, "1.0.0");
    private static readonly Counter<long> Upserts =
        Meter.CreateCounter<long>("tessera.preferences.upserts");
    // public Activity? StartUpsert(string userId, string key);
    // public void RecordUpsert(string outcome, double durationMs);
}

// repository:
public async Task<UserPreference> UpsertAsync(...)
{
    using var activity = UserPreferenceDiagnostics.StartUpsert(userId, key);
    var sw = Stopwatch.StartNew();
    try
    {
        // ...
        UserPreferenceDiagnostics.RecordUpsert("ok", sw.GetElapsedTime().TotalMilliseconds);
    }
    catch
    {
        UserPreferenceDiagnostics.RecordUpsert("error", sw.GetElapsedTime().TotalMilliseconds);
        throw;
    }
}
```

Tests use the standard `ActivityListener` + `MeterListener` API (OTel
SDK) to capture emissions:

```csharp
var captured = new List<Activity>();
using var al = new ActivityListener
{
    ShouldListenTo = src => src.Name == UserPreferenceDiagnostics.ModuleName,
    Sample = (_, _) => ActivitySamplingResult.AllDataAndRecorded,
    ActivityStarted = captured.Add,
};
ActivitySource.AddActivityListener(al);

using var _ = UserPreferenceDiagnostics.StartUpsert("alice", "theme");

Assert.Single(captured);
Assert.Equal("preferences.upsert", captured[0].OperationName);
```

Pros: matches rule, single source of truth, no DI overhead,
AOT-friendly, `Activity` is already disposable. `Tessera.*`
wildcard in `TesseraTelemetry.ConfigureTesseraTelemetry`
auto-discovers every module's source — zero per-module registration.

Cons: harder to mock in handler unit tests — test must use
`ActivityListener` / `MeterListener` rather than NSubstitute.

### Approach B — `IXxxDiagnostics` + DI

```csharp
public interface IUserPreferenceDiagnostics
{
    IDisposable? StartUpsert(string userId, string key);
    void RecordUpsert(string outcome, double durationMs);
}

public sealed class UserPreferenceDiagnostics(
    [FromKeyedServices("Tessera.Preferences")] ActivitySource activity,
    [FromKeyedServices("Tessera.Preferences")] Meter meter)
    : IUserPreferenceDiagnostics { /* ... */ }
```

Pros: mockable in handler tests (`Substitute.For<I*Diagnostics>()`),
can swap implementations (real vs in-memory recorder vs null-sink)
per-test, dependency is part of ctor argument list so passing
wrong diagnostics becomes a compile error.

Cons: 1 interface + 1 implementation per module, 1 DI registration
per module, 1 NSubstitute arg per repository/handler unit test,
mocking telemetry behavior can drift from real telemetry
implementation (the mocks say "yes this was called" but never
catch a real activity-name typo).

## Why Tessera chooses A right now (MVP-02)

Tessera's MVP-02 has no CQRS/worker pipeline:

| Module | Operations | Handler pattern |
|---|---|---|
| `Traces` | read-only proxy over Victoria | none (controller → Victoria client) |
| `Logs` | read-only proxy over Victoria | none |
| `Discovery` | read-only proxy over Victoria | none |
| `Health` | GET /api/v1/health | none |
| `Preferences` | `Get/Upsert/SoftDelete` on `UserPreferenceRepository` | direct repository call |

Per-module `IXxxDiagnostics` adds 4 files × 5 modules = 20 source
files plus 5 DI registrations plus per-test mock setup, but provides
zero diagnostic value in MVP-02 because **no module has a
behaviour pipeline to instrument**. The static-class approach
already satisfies Tessera's telemetry needs (counters + spans for
the proxy orchestration itself), and `ActivityListener` +
`MeterListener` cover the test path.

The escape clause in `observability/diagnostics.md` §1 ("Add that
weight only if there's a CQRS/worker pipeline") becomes relevant
when Tessera picks up Phase 8+ work (Dashboards with tiled
optimistic UI, Notification subsystem, scheduled jobs,
write-heavy Tenant settings). At that point the IC pattern pays
for itself.

## Acceptance criteria (for closing this issue)

1. `Tessera.Modules.Preferences/Persistence/Diagnostics/UserPreferenceDiagnostics.cs`
   exists, exposes `StartUpsert / StartGetByUserKey / StartSoftDelete`
   + matching `Record<Op>(string outcome, double durationMs)` methods.
2. `Meter` instance on `Tessera.Preferences` registers counters /
   histograms with names matching
   `tessera.preferences.<noun>.<quantity>` (per
   `observability/diagnostics.md` §3).
3. `ActivitySource` name is `Tessera.Preferences` (wildcard match in
   `TesseraTelemetry.ActivitySourceWildcard`).
4. No `IDiagnostics` interface in Tessera.Modules.Preferences
   namespace; repository calls static methods directly.
5. `Tessera.Modules.Preferences.Unit.Diagnostics.UserPreferenceDiagnosticsShould`
   covers: ActivityListener captures span name + module source,
   MeterListener captures Counter increment with right tag values.
6. Build clean, format clean, tests `n+2/121` passing (was `119`).

## Resumption checklist (for when to switch to B)

Adopt `IXxxDiagnostics` per module when **any** of these become true:

- A Tessera module acquires a `BackgroundService` / `IHostedService`
  worker that runs independently from the request path
  (notifications, retention sweeps, downstream sync).
- Tessera introduces an explicit `IRequestHandler<,>` / `ICommandHandler<,>`
  mediator with per-handler telemetry context (start span,
  record counters, capture exceptions).
- A single module has > 3 independent operations where handler
  tests want to assert *which* operation ran with *which* tags
  (typical for optimistic-concurrency write paths in Dashboards
  / Notifications).
- Tessera has two parallel module-internal paths that share the
  same operation name (e.g. "save" via DB vs via cache) and
  unit tests must distinguish which was called.

When the migration happens per module:

- Move the static-class fields into a single instance, registered
  as `AddSingleton<I<Module>Diagnostics, <Module>Diagnostics>()`.
- Replace `using var activity = UserPreferenceDiagnostics.StartUpsert(...)`
  with `_diagnostics.StartUpsert(...)` where `_diagnostics` is the
  new injected `IUserPreferenceDiagnostics`.
- Update unit tests from `ActivityListener` capture to NSubstitute
  `diagnostics.Received().Record<Op>(...)`.
- Update INTEGRATION tests / smoke probes (which still rely on real
  emissions through the host pipeline) to keep the
  ActivityListener pattern as a cross-check.

## Trade-offs revisited

| Aspect | A (static) | B (interface) |
|---|---|---|
| Files per module | 1 | 2 (+1 DI reg) |
| Mockability in unit tests | `ActivityListener` + `MeterListener` (stdlib, no NSubstitute) | `NSubstitute.For<I*Diagnostics>()` |
| DI cost | none | 1 ctor arg, +NMock arg in tests |
| Fakeable in tests | yes (ActivityListener is the SDK-provided fake) | yes |
| Source generator friendliness | OK (Meter / ActivitySource static fields are AOT-friendly) | OK |
| Drift risk (test mock ≠ real impl) | low (you exercise the real type) | medium (mocks drift) |
| Per-module registration boilerplate | 0 | 1 `AddSingleton` line |
| Test runs added (will look the same — ActivityListener) | yes | yes |

The dominant difference is **who sets up test dependencies**: A
adds a small amount of OTel SDK setup once per test; B adds one
constructor argument and one mock object per test. For a
proxy/CRUD module where the cost in either direction is small,
A wins on **proportionality** — the rule's exact criterion.

## Refs

- `~/.agents/rules/observability/diagnostics.md` §1 (escape clause
  language)
- `~/.agents/rules/observability/diagnostics.md` §3 (metric names)
- `~/.agents/rules/observability/diagnostics.md` §5 (don't
  double-instrument HTTP)
- `.agents/docs/observability/tessera-telemetry.md`
  (project-specific Tessera pipeline doc, MVP-02 status)
- ADR-0001 (storage and FS layout decisions; relevant only because
  Preferences storage is the first module-side instrument surface)
