using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Tessera.Shared.Telemetry;

/// <summary>
///     Single composition helper for Tessera's own OpenTelemetry
///     pipeline. Registered first in <c>Program.cs</c>, before any
///     other DI extension, so the pipeline sees the rest of the
///     host's bootstrap.
///     <para>
///         Per <c>observability/diagnostics.md</c> §1: one installer,
///         called once. The configuration lands in
///         <c>[telemetry]</c> of <c>tessera.toml</c> (see
///         <see cref="TelemetryOptions" />); the same installer
///         shapes traces + metrics with the same set of instruments
///         so what gets emitted stays coherent.
///     </para>
///     <para>
///         Module-level <see cref="System.Diagnostics.ActivitySource" />
///         declarations live in their own assemblies (one per
///         module, named after the module — see
///         <c>observability/diagnostics.md</c> §2). The shared
///         installer does not know about specific modules; the
///         host pipeline auto-discovers any <c>ActivitySource</c>
///         matching <c>Tessera.*</c> via <c>AddSource("Tessera.*")</c>.
///     </para>
/// </summary>
public static class TesseraTelemetry
{
    /// <summary>
    ///     Tessera-wide instrumentation source matching the
    ///     <c>Tessera.&lt;Module&gt;</c> convention. Modules declare
    ///     one <see cref="System.Diagnostics.ActivitySource" /> per
    ///     assembly named after the module; the host pipeline
    ///     subscribes to this wildcard source.
    /// </summary>
    public const string ActivitySourceWildcard = "Tessera.*";

    /// <summary>
    ///     Tessera-wide meter name (lowercase dot.case per
    ///     <c>observability/diagnostics.md</c> §3).
    /// </summary>
    public const string MeterName = "Tessera";

    /// <summary>
    ///     Configure Tessera's own OpenTelemetry pipeline from
    ///     <c>[telemetry]</c> section + the standard instrument set.
    ///     Call from <c>Program.cs</c>:
    ///     <code>
    ///     var telemetry = builder.Configuration
    ///         .GetSection(TelemetryOptions.SectionName)
    ///         .Get&lt;TelemetryOptions&gt;() ?? new TelemetryOptions();
    ///     builder.Services.AddOpenTelemetry()
    ///         .ConfigureTesseraTelemetry(telemetry);
    ///     </code>
    /// </summary>
    public static OpenTelemetry.OpenTelemetryBuilder ConfigureTesseraTelemetry(
        this OpenTelemetry.OpenTelemetryBuilder builder,
        TelemetryOptions options)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);

        return builder
            .ConfigureResource(resource => resource
                .AddService(serviceName: options.ServiceName, serviceVersion: options.ServiceVersion)
                .AddAttributes(
                [
                    new("deployment.environment",
                        Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "production"),
                ]))
            .WithTracing(tracing => ApplyTracing(tracing, options))
            .WithMetrics(metrics => ApplyMetrics(metrics, options));
    }

    private static void ApplyTracing(
        OpenTelemetry.Trace.TracerProviderBuilder tracing,
        TelemetryOptions options)
    {
        tracing
            .AddSource(ActivitySourceWildcard)
            .SetSampler(new TraceIdRatioBasedSampler(options.TraceSamplingRatio));

        if (options.EnableAspNetCoreInstrumentation)
        {
            tracing.AddAspNetCoreInstrumentation(asp =>
            {
                asp.RecordException = true;
                const string healthCheck = "/api/v1/health";
                asp.Filter = ctx => !ctx.Request.Path.Value?.StartsWith(healthCheck, StringComparison.Ordinal) ?? true;
            });
        }

        if (options.EnableHttpClientInstrumentation)
        {
            tracing.AddHttpClientInstrumentation();
        }

        tracing.AddOtlpExporter(otlp => otlp.Endpoint = new Uri(options.OtlpEndpoint));

        if (options.EnableConsoleExporter)
        {
            tracing.AddConsoleExporter();
        }
    }

    private static void ApplyMetrics(
        OpenTelemetry.Metrics.MeterProviderBuilder metrics,
        TelemetryOptions options)
    {
        metrics.AddMeter(MeterName);

        if (options.EnableAspNetCoreInstrumentation)
        {
            metrics.AddAspNetCoreInstrumentation();
        }

        if (options.EnableHttpClientInstrumentation)
        {
            metrics.AddHttpClientInstrumentation();
        }

        metrics.AddOtlpExporter(otlp => otlp.Endpoint = new Uri(options.OtlpEndpoint));

        if (options.EnableConsoleExporter)
        {
            metrics.AddConsoleExporter();
        }
    }
}
