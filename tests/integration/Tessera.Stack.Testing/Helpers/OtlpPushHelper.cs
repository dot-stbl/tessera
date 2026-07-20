using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Tessera.LoadGen;
using Tessera.LoadGen.Models;

namespace Tessera.Stack.Testing.Helpers;

/// <summary>
///     Pushes synthetic traces (and optional metrics) directly to the
///     OTel collector endpoint exposed by the compose stack. Tests
///     use this to seed data without running the
///     <c>Tessera.LoadGen</c> console process — running the same
///     code in-process keeps the test fast and deterministic.
/// </summary>
/// <remarks>
///     The collectors are created via
///     <see cref="Sdk.CreateTracerProviderBuilder" /> /
///     <see cref="Sdk.CreateMeterProviderBuilder" /> exactly as the
///     load generator does, so the OTel resource attributes are
///     identical whether the data came from the CLI or the test.
/// </remarks>
public static class OtlpPushHelper
{
    /// <summary>
    ///     For <paramref name="duration" />, emit one synthetic span
    ///     per 100 ms tick at the supplied OTLP endpoint. The traces
    ///     are visible to <c>Tessera.Host</c> once the OTel collector
    ///     fans them out to <c>VictoriaTraces</c>.
    /// </summary>
    /// <returns>Number of traces pushed.</returns>
    public static async Task<int> PushTracesAsync(
        Uri otlpEndpoint,
        IEnumerable<SyntheticService> services,
        TimeSpan duration,
        CancellationToken cancellationToken = default)
    {
        var serviceList = services.ToList();
        if (serviceList.Count == 0)
        {
            return 0;
        }

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(Generator.SourceName)
            .AddOtlpExporter(otlp =>
            {
                otlp.Endpoint = otlpEndpoint;
                otlp.Protocol = OtlpExportProtocol.Grpc;
            })
            .Build();

        var activitySource = new ActivitySource(Generator.SourceName);
        var startTime = DateTimeOffset.UtcNow;
        var traceCount = 0;
        var random = new Random(0xBEEF);

        while (DateTimeOffset.UtcNow - startTime < duration && !cancellationToken.IsCancellationRequested)
        {
            var service = serviceList[random.Next(serviceList.Count)];
            var operation = service.Operations[random.Next(service.Operations.Count)];

            using (var activity = activitySource.StartActivity($"{operation}", ActivityKind.Server))
            {
                activity?.SetTag("service.name", service.Name);
                activity?.SetTag("app.scenario", "test");
                await Task.Delay(50, cancellationToken).ConfigureAwait(false);
            }

            traceCount++;
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
        }

        tracerProvider.ForceFlush(5_000);
        return traceCount;
    }

    /// <summary>
    ///     Emits a single synthetic <see cref="Activity" />
    ///     synchronously. The caller is responsible for managing
    ///     the <see cref="ActivitySource" /> lifetime and the OTel
    ///     exporter flush cadence — useful for tests that want
    ///     fine-grained control over when a trace is published.
    ///     </summary>
    public static void PushSingleTrace(
        ActivitySource activitySource,
        string operation,
        string serviceName)
    {
        using var activity = activitySource.StartActivity(operation, ActivityKind.Server);
        activity?.SetTag("service.name", serviceName);
        activity?.SetTag("app.scenario", "single");
    }
}
