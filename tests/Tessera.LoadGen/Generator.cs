using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Tessera.LoadGen.Models;
using Tessera.LoadGen.Scenarios;

namespace Tessera.LoadGen;

/// <summary>
///     Synthetic OTel telemetry generator. Configures the
///     <see cref="OpenTelemetry" /> SDK with an OTLP exporter and
///     pumps <see cref="Activity" />, log records, and RED metrics
///     at the configured rate for the configured duration.
/// </summary>
/// <remarks>
///     <para>
///         Public so that integration tests can call it in-process
///         from a <c>xunit</c> fixture (see
///         <c>Tessera.Stack.Integration</c>) — runs faster than
///         shelling out to <c>dotnet run</c> against the same CLI.
///     </para>
///     <para>
///         The <c>ActivitySource</c> and <c>Meter</c> names are
///         fixed to <c>Tessera.LoadGen</c> so the OTel collector
///         can attribute the produced telemetry by source.
///     </para>
/// </remarks>
public sealed class Generator
{
    /// <summary>OTel <see cref="ActivitySource" /> name. Filterable downstream.</summary>
    public const string SourceName = "Tessera.LoadGen";

    /// <summary>OTel <see cref="Meter" /> name. Filterable downstream.</summary>
    public const string MeterName = "Tessera.LoadGen";

    private static readonly ActivitySource ActivitySource = new(SourceName);
    private static readonly Meter Meter = new(MeterName);

    /// <summary>Executes <paramref name="options" /> until <paramref name="cancellationToken" /> fires or <see cref="GeneratorOptions.Duration" /> elapses.</summary>
    public static async Task<int> RunAsync(GeneratorOptions options, CancellationToken cancellationToken = default)
    {
        var kind = options.Scenario.ToScenarioKind();
        var services = DefaultServiceFactory.Materialise(options.Services);

        // RED metrics — counts per outcome and latency histogram per
        // operation. Bound to the active Meter instance so the
        // OtlpExporter attaches the correct resource attributes.
        var requestCounter = Meter.CreateCounter<long>(
            "tessera.loadgen.operations",
            unit: "{operation}",
            description: "Number of synthetic operations emitted by Tessera.LoadGen.");
        var durationHistogram = Meter.CreateHistogram<double>(
            "tessera.loadgen.operation.duration",
            unit: "ms",
            description: "Wall-clock duration of a synthetic operation.");

        var resourceAttributes = new[]
        {
            new KeyValuePair<string, object>("service.name", SourceName),
            new KeyValuePair<string, object>("deployment.environment", "development"),
        };

        using var loggerFactory = LoggerFactory.Create(builder => builder.AddOpenTelemetry(logging =>
        {
            logging.SetResourceBuilder(ResourceBuilder.CreateEmpty().AddService(SourceName).AddAttributes(resourceAttributes));
            logging.AddOtlpExporter(otlp =>
            {
                otlp.Endpoint = new UriBuilder(options.OtlpEndpoint).Uri;
                otlp.Protocol = OtlpExportProtocol.Grpc;
            });
        }));
        var logger = loggerFactory.CreateLogger<Generator>();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .SetResourceBuilder(ResourceBuilder.CreateEmpty().AddService(SourceName).AddAttributes(resourceAttributes))
            .SetSampler(new TraceIdRatioBasedSampler(1.0))
            .AddSource(SourceName)
            .AddOtlpExporter(otlp =>
            {
                otlp.Endpoint = new UriBuilder(options.OtlpEndpoint).Uri;
                otlp.Protocol = OtlpExportProtocol.Grpc;
            })
            .Build();
        using var meterProvider = options.PushMetrics
            ? Sdk.CreateMeterProviderBuilder()
                .SetResourceBuilder(ResourceBuilder.CreateEmpty().AddService(SourceName).AddAttributes(resourceAttributes))
                .AddMeter(MeterName)
                .AddOtlpExporter(otlp =>
                {
                    otlp.Endpoint = new UriBuilder(options.OtlpEndpoint).Uri;
                    otlp.Protocol = OtlpExportProtocol.Grpc;
                })
                .Build()
            : null;

        logger.LogInformation(
            "Tessera.LoadGen starting: endpoint={Endpoint}, services={ServiceCount}, rate={Rate}/s, scenario={Scenario}, duration={Duration}",
            options.OtlpEndpoint,
            services.Count,
            options.Rate,
            kind,
            options.Duration);

        var deadline = options.Duration == TimeSpan.Zero
            ? DateTimeOffset.MaxValue
            : DateTimeOffset.UtcNow.Add(options.Duration);
        var random = new Random(0xC0FFEE);
        var totalOperations = 0L;
        var tickInterval = options.Rate <= 0 ? TimeSpan.FromSeconds(1) : TimeSpan.FromSeconds(1.0 / options.Rate);
        var nextTick = DateTimeOffset.UtcNow;

        while (DateTimeOffset.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
        {
            var now = DateTimeOffset.UtcNow;
            if (now < nextTick)
            {
                await Task.Delay(nextTick - now, cancellationToken);
            }
            nextTick = nextTick.Add(tickInterval);

            var service = services[random.Next(services.Count)];
            var operation = service.Operations[random.Next(service.Operations.Count)];
            var iteration = new ScenarioIteration(
                Service: service,
                Operation: operation,
                ActivitySource: ActivitySource,
                Logger: logger,
                Random: random);

            var startedAt = Stopwatch.GetTimestamp();
            bool ok;
            try
            {
                ok = await ScenarioDispatcher.ExecuteAsync(kind, iteration, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Scenario iteration threw for {ServiceName}.{Operation}", service.Name, operation);
                ok = false;
            }

            var elapsedMs = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
            requestCounter.Add(1, new KeyValuePair<string, object?>("service.name", service.Name), new KeyValuePair<string, object?>("outcome", ok ? "ok" : "error"));
            durationHistogram.Record(elapsedMs, new KeyValuePair<string, object?>("service.name", service.Name));
            totalOperations++;
        }

        logger.LogInformation("Tessera.LoadGen stopping after {Operations} operations", totalOperations);

        // Force-flush so the last batch of spans/logs/metrics reaches
        // the collector before the process exits.
        tracerProvider?.ForceFlush(5_000);
        meterProvider?.ForceFlush(5_000);
        return 0;
    }
}
