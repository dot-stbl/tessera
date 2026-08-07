using System.ComponentModel.DataAnnotations;

namespace Tessera.Shared.Telemetry;

/// <summary>
///     Tessera's own OpenTelemetry signal configuration. Bound from
///     the <c>[telemetry]</c> TOML table by the host composition
///     root; <see cref="TesseraTelemetry.ConfigureTesseraTelemetry" />
///     wires the result into a single
///     <see cref="OpenTelemetry.Trace.TracerProviderBuilder" /> +
///     <see cref="OpenTelemetry.Metrics.MeterProviderBuilder" />
///     pipeline.
///     <para>
///         Per <c>observability/diagnostics.md</c>: one
///         <see cref="System.Diagnostics.ActivitySource" /> per
///         assembly, namespaced <c>Tessera.&lt;Module&gt;</c>;
///         meter names <c>tessera.&lt;noun&gt;.&lt;quantity&gt;</c>;
///         bounded-cardinality tags (dot.case, no PII). HTTP
///         transport is auto-instrumented by
///         <c>OpenTelemetry.Instrumentation.AspNetCore</c> +
///         <c>OpenTelemetry.Instrumentation.Http</c> — handlers and
///         Refit clients do not need their own
///         <c>StartActivity</c> wrappers (the auto-instrumentation
///         already produces per-request spans).
///     </para>
///     <para>
///         OTLP exporter only ships spans / metrics / logs to the
///         collector configured in
///         <c>compose/otel-collector/config.yaml</c> — not to any
///         individual provider storage. The collector's
///         <c>prometheusremotewrite</c> + <c>otlphttp</c> exporters
///         then route each signal to its native Victoria endpoint.
///     </para>
/// </summary>
public sealed class TelemetryOptions
{
    /// <summary>Configuration section name in <c>tessera.toml</c>.</summary>
    public const string SectionName = "telemetry";

    /// <summary>
    ///     OpenTelemetry service name. Default <c>"tessera"</c>.
    /// </summary>
    public string ServiceName { get; init; } = "tessera";

    /// <summary>
    ///     OpenTelemetry service version. Default reads
    ///     <see cref="System.Reflection.AssemblyInformationalVersionAttribute" />
    ///     on the host assembly.
    /// </summary>
    public string ServiceVersion { get; init; } = "0.0.0";

    /// <summary>
    ///     OTLP endpoint. Default <c>http://localhost:4317</c>
    /// (gRPC). Compose-stack collector listens there.
    /// </summary>
    public string OtlpEndpoint { get; init; } = "http://localhost:4317";

    /// <summary>
    ///     Emit a console exporter alongside OTLP. Default
    ///     <c>false</c>; the OTLP line is enough for operator-side
    ///     verification, and the console exporter doubles the
    ///     export cost.
    /// </summary>
    public bool EnableConsoleExporter { get; init; }

    /// <summary>
    ///     Auto-instrument ASP.NET Core requests. Default <c>true</c>.
    ///     Off for unit-test loops or when a side-car proxy (e.g.
    ///     OTel Collector sidecar via <c>--otel-exporter-otlp-traces-protocol
    ///     =grpc</c>) already covers HTTP.
    /// </summary>
    public bool EnableAspNetCoreInstrumentation { get; init; } = true;

    /// <summary>
    ///     Auto-instrument outbound <see cref="HttpClient" /> calls.
    ///     Default <c>true</c>. Per <c>observability/diagnostics.md</c>
    ///     §5: do not double-instrument with manual
    ///     <c>using var activity = source.StartActivity(...)</c> around
    ///     HTTP calls — auto-instrumentation already produces the per-
    ///     request span.
    /// </summary>
    public bool EnableHttpClientInstrumentation { get; init; } = true;

    /// <summary>
    ///     Sampling ratio applied to the root trace (1.0 = always on;
    ///     0.0 = all dropped). Default
    ///     <c>1.0 (ParentBased(AlwaysOn))</c>. Operators tune this
    ///     down in production to control cost; tests / dev keep it
    ///     at 1.0.
    /// </summary>
    [Range(0.0, 1.0)]
    public double TraceSamplingRatio { get; init; } = 1.0;
}
