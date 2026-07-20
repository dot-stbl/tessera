namespace Tessera.LoadGen;

/// <summary>
///     <c>Generator</c> command-line arguments. Bound by
///     <c>GeneratorOptionsParser.Parse</c>, passed by-value to
///     <c>Generator.RunAsync(...)</c>.
/// </summary>
/// <remarks>
///     Defaults match the developer-local happy path: OTel collector at
///     <c>http://localhost:4317</c>, four synthetic services, ten
///     operations per second, the <c>simple</c> scenario shape. The
///     <see cref="Scenario" /> value is parsed once by <c>Generator.RunAsync</c>
///     into <c>ScenarioKind</c> — invalid values surface as
///     <see cref="ArgumentException" /> at run time, not at parse time, to
///     keep the parser dependency-free.
/// </remarks>
public sealed record GeneratorOptions
{
    /// <summary>OTLP endpoint URL (gRPC preferred, HTTP fallback). Default <c>http://localhost:4317</c>.</summary>
    public string OtlpEndpoint { get; init; } = "http://localhost:4317";

    /// <summary>Synthetic service names emitted by the load generator. Default four-service fanout.</summary>
    public IReadOnlyList<string> Services { get; init; } =
    [
        "checkout-svc",
        "payment-svc",
        "auth-svc",
        "notification-svc",
    ];

    /// <summary>Operations per second, distributed evenly across <see cref="Services" />. Default <c>10</c>.</summary>
    public int Rate { get; init; } = 10;

    /// <summary>Total wall-clock duration of the run. Default <see cref="TimeSpan.Zero" /> means "until cancelled".</summary>
    public TimeSpan Duration { get; init; } = TimeSpan.Zero;

    /// <summary>Scenario name — <c>simple</c>, <c>fanout</c>, or <c>saga</c>. Default <c>simple</c>.</summary>
    public string Scenario { get; init; } = "simple";

    /// <summary>Whether to emit RED metrics alongside traces. Default <c>true</c>.</summary>
    public bool PushMetrics { get; init; } = true;

    /// <summary>Whether to emit log records correlated with the active trace. Default <c>true</c>.</summary>
    public bool PushLogs { get; init; } = true;
}

/// <summary>
///     Positional / flag parser for <see cref="GeneratorOptions" />.
///     Co-located with the record because the parser is the only
///     consumer of <see cref="GeneratorOptions" />'s shape and adding
///     a <c>System.CommandLine</c> dependency for an internal CLI
///     adds more weight than it saves.
/// </summary>
internal static class GeneratorOptionsParser
{
    /// <summary>
    ///     Parses <c>args</c> into <see cref="GeneratorOptions" />.
    ///     Unknown flags throw <see cref="ArgumentException" />;
    ///     missing values throw <see cref="ArgumentException" /> with
    ///     the option name in the message.
    /// </summary>
    /// <exception cref="ArgumentException"></exception>
    public static GeneratorOptions Parse(string[] args)
    {
        var otlpEndpoint = "http://localhost:4317";
        var services = new[]
        {
            "checkout-svc",
            "payment-svc",
            "auth-svc",
            "notification-svc",
        };
        var rate = 10;
        var duration = TimeSpan.Zero;
        var scenario = "simple";
        var pushMetrics = true;
        var pushLogs = true;

        for (var index = 0; index < args.Length; index++)
        {
            var flag = args[index];
            var value = index + 1 < args.Length ? args[index + 1] : null;
            switch (flag)
            {
                case "--endpoint":
                    otlpEndpoint = RequireValue(flag, value);
                    break;
                case "--services":
                    services = RequireValue(flag, value).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    break;
                case "--rate":
                    rate = int.Parse(RequireValue(flag, value), System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case "--duration":
                    duration = TimeSpan.Parse(RequireValue(flag, value), System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case "--scenario":
                    scenario = RequireValue(flag, value);
                    break;
                case "--no-metrics":
                    pushMetrics = false;
                    break;
                case "--no-logs":
                    pushLogs = false;
                    break;
                case "-h":
                case "--help":
                    PrintUsage();
                    Environment.Exit(0);
                    break;
                default:
                    throw new ArgumentException($"Unknown option: {flag}", nameof(args));
            }
            index++;
        }

        return new GeneratorOptions
        {
            OtlpEndpoint = otlpEndpoint,
            Services = services,
            Rate = rate,
            Duration = duration,
            Scenario = scenario,
            PushMetrics = pushMetrics,
            PushLogs = pushLogs,
        };
    }

    private static string RequireValue(string flag, string? value)
    {
        if (string.IsNullOrEmpty(value) || value.StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException($"Option '{flag}' requires a value.", nameof(value));
        }
        return value;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Tessera.LoadGen — synthetic OTel telemetry generator");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  Tessera.LoadGen [--endpoint URL] [--services a,b,c]");
        Console.WriteLine("                  [--rate N] [--duration 30s]");
        Console.WriteLine("                  [--scenario simple|fanout|saga]");
        Console.WriteLine("                  [--no-metrics] [--no-logs]");
        Console.WriteLine();
        Console.WriteLine("Defaults if a flag is omitted:");
        Console.WriteLine("  --endpoint http://localhost:4317");
        Console.WriteLine("  --services checkout-svc,payment-svc,auth-svc,notification-svc");
        Console.WriteLine("  --rate 10");
        Console.WriteLine("  --scenario simple");
        Console.WriteLine();
        Console.WriteLine("Notes:");
        Console.WriteLine("  --duration 0 (the default) means \"until Ctrl+C\".");
        Console.WriteLine("  --no-metrics / --no-logs disable the corresponding pipeline.");
    }
}
