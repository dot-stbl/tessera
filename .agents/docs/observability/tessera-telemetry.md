# Tessera — own OpenTelemetry pipeline

> **Status:** Phase 7a DONE. Wired through `Tessera.Shared.Telemetry`
> in `tessera/mvp-2-platform` (commit `b30da28`). Deployed via the
> `compose/Dockerfile` multi-stage build; collector endpoint in
> `compose/otel-collector/config.yaml`; FlowGen (`tests/Tessera.LoadGen`)
> + Tessera host + any third-party OTLP-aware client all push to the
> same collector.

This document is the project-specific companion to the global
`observability/diagnostics.md`. **Read the global rule first** —
this file only captures Tessera's specific layout, conventions,
and operator-facing knobs.

## What gets emitted

```
┌──────────────────┐
│ Tessera.Host      │ OTLP/HTTP + OTLP/gRPC
│ Tessera.LoadGen   ├────────────────► otel-collector:4317/4318
│ third-party       │                    │
│ OTel SDKs         │                    ▼
└──────────────────┘                exporters:
                                       ├─► prometheusremotewrite → VictoriaMetrics (metrics)
                                       ├─► otlphttp           → VictoriaLogs    (logs)
                                       ├─► otlphttp           → VictoriaTraces  (traces)
                                       └─► debug             → stdout          (dev only)
```

The collector routes each signal type to its native Victoria endpoint via
the per-pipeline `<metrics|traces|logs>` block in
`compose/otel-collector/config.yaml`. Tessera itself does **not** talk
to Victoria directly — only the collector does.

## Configuration (`[telemetry]` in `tessera.toml`)

```toml
[telemetry]
service_name                       = "tessera"
service_version                    = "0.0.0"
otlp_endpoint                      = "http://localhost:4317"
enable_console_exporter            = false   # dev only
enable_asp_net_core_instrumentation = true
enable_http_client_instrumentation = true
trace_sampling_ratio               = 1.0      # 0.05–0.1 in production
```

All keys have data-annotation validation
(`TelemetryOptions`); `[telemetry]/service_name` is required.
Defaults are in `src/shared/Tessera.Shared.Telemetry/TelemetryOptions.cs`
— document conventions for the operator-facing surface live here.

### Health probes are not traces

`/api/v1/health` is filtered out of the ASP.NET Core instrumentation
in `TesseraTelemetry.ApplyTracing` — health-check probes poll the
endpoint every few seconds and would otherwise dominate the trace
stream with spans that carry no diagnostic value. If you need to
trace health checks for a specific incident, flip
`EnableAspNetCoreInstrumentation` temporarily.

## Convention for new code (Tessera-specific)

Per the global rule:

| Surface | Naming |
|---|---|
| `ActivitySource` | `Tessera.<Module>` — declared in each module's Persistence/Trace and registered via the `Tessera.*` wildcard in `TesseraTelemetry.ConfigureTesseraTelemetry` |
| `Meter` | Single `Tessera` meter on the host; module-level metrics piggy-back on the same meter (don't `AddMeter("Tessera.X")` per module) |
| Metric names | `tessera.<noun>.<quantity>` (lowercase, dot.case; noun-level tags); unit goes in the instrument declaration, not the name |
| Tags | dot.case bounded-cardinality (`provider.name`, `upstream.name`, `module.name`, `error.type`); never `id`s / query text / PII |

```csharp
// Module-level — one ActivitySource per Persistence layer.
public sealed class TraceRepository(PreferencesDbContext dbContext)
    : Repository<UserPreference>(dbContext)
{
    private static readonly ActivitySource Activity = new("Tessera.Preferences");
    // ...
    public async Task<UserPreference> UpsertAsync(...)
    {
        using var activity = Activity.StartActivity("preferences.upsert");
        activity?.SetTag("preferences.key", key);
        activity?.SetTag("user.id", userId);   // user.id is bounded: 1 user per request
        // ...
    }
}
```

> **Bounded-cardinality reminder.** `user.id` is a per-request
> string but cardinality is "1 user per request", so the
> cardinality spread is bounded by active users (still bad in
> practice — prefer "anonymous" / "authenticated" booleans).
> Never tag with `order.id`, `trace.id` (raw 32-hex), or free
> query text.

## Where Tessera's exports end up

The collector in `compose/otel-collector/` is a `config.yaml`-driven
OTel Collector binary (`otelcol` image). Tessera's own
`AddOtlpExporter(...)` lines from `TesseraTelemetry` push via gRPC at
port 4317 by default. Each signal has its own exporter:

| Tessera signal | `<exporter>` in collector config | Destination |
|---|---|---|
| traces (OTLP gRPC) | `otlphttp/VictoriaTraces` | `http://victoria-traces:10428/insert/opentelemetry/v1/traces` |
| metrics (OTLP PeriodicExportingMetricReader, 5s) | `prometheusremotewrite/VictoriaMetrics` | `http://victoria-metrics:8429/api/v1/write` |
| logs (Future — `WithLogging`) | `otlphttp/VictoriaLogs` | `http://victoria-logs:9428/insert/opentelemetry/v1/logs` |

Logs are not yet wired through `TesseraTelemetry` (deferred — see
below).

## Deferred / out-of-scope for MVP-02

- **`WithLogging(...)` integration.** Would require
  `OpenTelemetry.Extensions.Logging` + a small log bridge into OTLP
  spans via `OpenTelemetryLoggerOptions.IncludeScopes = true`.
  Deferred so the OTel package surface stays minimal (the dependency
  tree is large enough already without `OpenTelemetry.Exporter.Console`
  + `OpenTelemetry.Extensions.Hosting` + the runtime/logs
  instrumentations).
- **Per-module instrumentation sets.** Right now Tessera.* follows a
  wildcard; if a module needs an instrument that's specific to it
  (e.g. EF Core command traces filtered to that module's DbContext),
  it'll add a dedicated `AddSource("Tessera.<Module>")` in a future
  Phase and remove it from the wildcard.

## Where to look when something goes wrong

1. **No traces in Victoria Traces?** Check `compose/otel-collector` is
   running (`docker compose logs otel-collector`); check
   `[telemetry]/otlp_endpoint` matches the collector (default
   `http://localhost:4317` works in `compose/docker-compose.yml`
   because Tessera and otel-collector share the bridge network and
   can hit `otel-collector:4317`). If Tessera runs outside
   compose, point OTLP at a reachable host.
2. **CPU spikes in the host?** Check
   `trace_sampling_ratio` — `1.0` (default) keeps every trace.
   Drop to `0.05`–`0.1` for high-RPS deployments.
3. **Too many `Tessera.*` sources registered / not appearing?** Each
   new `ActivitySource("Tessera.<X>")` declaration is auto-picked-up
   via the `AddSource("Tessera.*")` in
   `TesseraTelemetry.ApplyTracing`. If a source needs to be
   disabled, change `ActivitySourceWildcard` to a more selective
   filter.
