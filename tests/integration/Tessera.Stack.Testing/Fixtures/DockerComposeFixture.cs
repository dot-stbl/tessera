using System.Diagnostics;

namespace Tessera.Stack.Testing.Fixtures;

/// <summary>
///     Pulls up the compose stack from <c>compose/docker-compose.yml</c>
///     via the <c>docker compose</c> CLI and exposes the URLs the
///     integration tests need. The fixture only requests the data-plane
///     services that the tests actually exercise
///     (<c>victoria-metrics</c>, <c>victoria-logs</c>,
///     <c>victoria-traces</c>, <c>otel-collector</c>) — the
///     <c>tessera</c> service is brought up by the
///     <see cref="TesseraStackFixture" /> in the test process so the
///     integration test owns the host lifecycle (WAF start/stop).
/// </summary>
/// <remarks>
///     <para>
///         Compose v2 supports <c>up --wait</c> which blocks until all
///         healthchecks pass; that single command replaces the
///         boilerplate of polling each container's <c>/health</c>
///         endpoint. CI / local-dev environments need a working
///         <c>docker</c> CLI on PATH — the integration tests in
///         <c>Tessera.Stack.Integration</c> opt into
///         <c>TESSERA_INTEGRATION=1</c> to skip the run when the
///         stack is unavailable.
///     </para>
///     <para>
///         Each fixture lifetime pays the cold-start cost of pulling
///         images and starting 4 containers, so the fixture is shared
///         across all integration tests in the collection via xUnit's
///         <c>IClassFixture&lt;&gt;</c> pattern. <see cref="IDisposable.Dispose" />
///         tears the stack down; tests that only need to read state
///         can short-circuit <see cref="EnsureRunningAsync" />.
///     </para>
/// </remarks>
/// <remarks>Constructor overload for explicit file path.</remarks>
public sealed class DockerComposeFixture(string composeFile, string[]? services = null) : IDisposable
{
    private const string DefaultComposeFile = "compose/docker-compose.yml";

    private static readonly string[] RequiredServices =
    [
        "victoria-metrics",
        "victoria-logs",
        "victoria-traces",
        "otel-collector",
    ];

    private readonly string _composeFile = Path.GetFullPath(composeFile);
    private readonly string[] _services = services ?? RequiredServices;
    private int _running;

    /// <summary>Public HTTP endpoint for VictoriaMetrics on the compose network.</summary>
    public Uri VictoriaMetricsUrl { get; } = new("http://localhost:8428");

    /// <summary>Public HTTP endpoint for VictoriaLogs on the compose network.</summary>
    public Uri VictoriaLogsUrl { get; } = new("http://localhost:9428");

    /// <summary>Public HTTP endpoint for VictoriaTraces on the compose network.</summary>
    public Uri VictoriaTracesUrl { get; } = new("http://localhost:10428");

    /// <summary>Public OTLP/gRPC endpoint for the OTel collector on the compose network.</summary>
    public Uri OtlpEndpointUrl { get; } = new("http://localhost:4317");

    /// <summary>
    ///     Constructs the fixture against the standard
    ///     <c>compose/docker-compose.yml</c>. The constructor does not
    ///     invoke <c>docker compose</c>; call
    ///     <see cref="EnsureRunningAsync" /> from a static
    ///     <c>[Fact]</c> precondition or rely on the
    ///     <c>Tessera.Stack.Integration</c> test class's
    ///     integration-gating attribute to skip the test entirely.
    /// </summary>
    public DockerComposeFixture()
        : this(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", DefaultComposeFile))
    {
    }

    /// <summary>
    ///     Idempotent. Runs <c>docker compose -f {file} up -d --wait
    ///     {services}</c> on first call; subsequent calls are no-ops
    ///     (returns immediately). <c>--wait</c> blocks the CLI until
    ///     every healthcheck reports healthy, so a successful return
    ///     means the stack is ready to receive traffic.
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    public async Task EnsureRunningAsync()
    {
        if (Interlocked.Exchange(ref _running, 1) != 0)
        {
            return;
        }

        var startInfo = new ProcessStartInfo("docker")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("compose");
        startInfo.ArgumentList.Add("-f");
        startInfo.ArgumentList.Add(_composeFile);
        startInfo.ArgumentList.Add("up");
        startInfo.ArgumentList.Add("-d");
        startInfo.ArgumentList.Add("--wait");
        startInfo.ArgumentList.Add("--wait-timeout");
        startInfo.ArgumentList.Add("120");
        foreach (var service in _services)
        {
            startInfo.ArgumentList.Add(service);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start docker compose process.");
        var stderr = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
        await process.WaitForExitAsync().ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            Interlocked.Exchange(ref _running, 0);
            throw new InvalidOperationException(
                $"docker compose up failed (exit {process.ExitCode}): {stderr}");
        }
    }

    /// <summary>
    ///     Runs <c>docker compose -f {file} down -v</c>. Anonymous
    ///     volumes are dropped so the next test starts with clean
    ///     Victoria state.
    /// </summary>
    public void Dispose()
    {
        var startInfo = new ProcessStartInfo("docker")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("compose");
        startInfo.ArgumentList.Add("-f");
        startInfo.ArgumentList.Add(_composeFile);
        startInfo.ArgumentList.Add("down");
        startInfo.ArgumentList.Add("-v");
        startInfo.ArgumentList.Add("--remove-orphans");

        using var process = Process.Start(startInfo);
        process?.WaitForExit(30_000);
    }
}
