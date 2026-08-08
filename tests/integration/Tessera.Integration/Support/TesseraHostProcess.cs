using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Tessera.Integration.Support;

/// <summary>
///     Spawns <c>Tessera.Host</c> against a temp <c>tessera.toml</c> that points
///     at the integration Victoria stack. Disposes by killing the process PID.
/// </summary>
public sealed class TesseraHostProcess : IAsyncDisposable
{
    private readonly string workDirectory;
    private readonly StringBuilder stdOut = new();
    private readonly StringBuilder stdErr = new();

    private TesseraHostProcess(Process process, string workDirectory, Uri baseAddress)
    {
        Process = process;
        this.workDirectory = workDirectory;
        BaseAddress = baseAddress;
    }

    /// <summary>Loopback base URL including free port (e.g. <c>http://127.0.0.1:5xxx/</c>).</summary>
    public Uri BaseAddress { get; }

    /// <summary>
    ///     Start host with Victoria URLs from <see cref="VictoriaEndpoints" />,
    ///     wait until health returns 200 (or throw with captured logs).
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    public static async Task<TesseraHostProcess> StartAsync(CancellationToken cancellationToken = default)
    {
        var port = TesseraHostProcessHelpers.FindFreePort();
        var workDirectory = Path.Combine(Path.GetTempPath(), "tessera-it-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDirectory);
        var dataDir = Path.Combine(workDirectory, "data");
        Directory.CreateDirectory(dataDir);

        var configPath = Path.Combine(workDirectory, "tessera.toml");
        var dbPath = Path.Combine(dataDir, "tessera-it.db").Replace('\\', '/');
        await File.WriteAllTextAsync(
            configPath,
            TesseraHostProcessHelpers.BuildToml(port, dbPath),
            cancellationToken);

        var hostProject = TesseraHostProcessHelpers.ResolveHostProjectPath();
        var startInfo = TesseraHostProcessHelpers.CreateStartInfo(hostProject, workDirectory, configPath, dataDir);

        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        var host = new TesseraHostProcess(process, workDirectory, new Uri($"http://127.0.0.1:{port}/"));
        process.OutputDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is not null)
            {
                lock (host.stdOut)
                {
                    host.stdOut.AppendLine(eventArgs.Data);
                }
            }
        };
        process.ErrorDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is not null)
            {
                lock (host.stdErr)
                {
                    host.stdErr.AppendLine(eventArgs.Data);
                }
            }
        };

        if (!process.Start())
        {
            throw new InvalidOperationException("failed to start Tessera.Host process");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await TesseraHostProcessHelpers.WaitForHealthAsync(host, cancellationToken);
        }
        catch
        {
            await host.DisposeAsync();
            throw;
        }

        return host;
    }

    /// <summary>HTTP client pre-bound to <see cref="BaseAddress" />.</summary>
    public HttpClient CreateClient()
    {
        return new HttpClient
        {
            BaseAddress = BaseAddress,
            Timeout = TimeSpan.FromSeconds(30),
        };
    }

    /// <summary>Stdout captured while the process ran (for failure diagnostics).</summary>
    public string StdOutSnapshot()
    {
        lock (stdOut)
        {
            return stdOut.ToString();
        }
    }

    /// <summary>Stderr captured while the process ran (for failure diagnostics).</summary>
    public string StdErrSnapshot()
    {
        lock (stdErr)
        {
            return stdErr.ToString();
        }
    }

    /// <summary>Underlying process (for exit checks during health wait).</summary>
    public Process Process { get; }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        try
        {
            if (!Process.HasExited)
            {
                Process.Kill(entireProcessTree: true);
                await Process.WaitForExitAsync();
            }
        }
        catch
        {
            // best-effort teardown
        }
        finally
        {
            Process.Dispose();
            try
            {
                if (Directory.Exists(workDirectory))
                {
                    Directory.Delete(workDirectory, recursive: true);
                }
            }
            catch
            {
                // temp cleanup best-effort
            }
        }
    }
}

/// <summary>Helpers for <see cref="TesseraHostProcess" /> (file-static).</summary>
file static class TesseraHostProcessHelpers
{
    public static async Task WaitForHealthAsync(TesseraHostProcess host, CancellationToken cancellationToken)
    {
        using var client = host.CreateClient();
        var deadline = DateTimeOffset.UtcNow.AddSeconds(90);
        Exception? last = null;

        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (host.Process.HasExited)
            {
                throw new InvalidOperationException(
                    $"Tessera.Host exited {host.Process.ExitCode} before healthy.\nstdout:\n{host.StdOutSnapshot()}\nstderr:\n{host.StdErrSnapshot()}");
            }

            try
            {
                using var response = await client.GetAsync("api/v1/health", cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }

                last = new InvalidOperationException($"health {(int)response.StatusCode}");
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                last = exception;
            }

            await Task.Delay(500, cancellationToken);
        }

        throw new TimeoutException(
            $"Tessera.Host did not become healthy at {host.BaseAddress}. last={last}\nstdout:\n{host.StdOutSnapshot()}\nstderr:\n{host.StdErrSnapshot()}");
    }

    /// <summary>
    ///     Pick a free loopback port inside the Tessera host pool (1990–2120).
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    public static int FindFreePort()
    {
        for (var port = 1995; port <= 2120; port++)
        {
            var listener = new TcpListener(IPAddress.Loopback, port);
            try
            {
                listener.Start();
                return port;
            }
            catch (SocketException)
            {
                // in use — try next
            }
            finally
            {
                try
                {
                    listener.Stop();
                }
                catch
                {
                    // ignore
                }
            }
        }

        throw new InvalidOperationException("no free port in Tessera pool 1995–2120");
    }

    public static ProcessStartInfo CreateStartInfo(
        string hostProject,
        string workDirectory,
        string configPath,
        string dataDir)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{hostProject}\" -c Debug --no-launch-profile",
            WorkingDirectory = workDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        startInfo.Environment["TESSERA_CONFIG"] = configPath;
        startInfo.Environment["TESSERA_DATA_PATH"] = dataDir;
        // Production: Development enables DI ValidateScopes, and providers still
        // inject concrete VictoriaOptions (not IOptions<>) — that fails host start.
        // Match the verified manual smoke (Production + TESSERA_CONFIG).
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
        startInfo.Environment["DOTNET_ENVIRONMENT"] = "Production";
        return startInfo;
    }

    public static string BuildToml(int port, string sqlitePath)
    {
        return $"""
                [server]
                host = "127.0.0.1"
                port = {port}

                [victoria]
                tenant = "0"
                timeout_ms = 15000

                [victoria.traces]
                url = "{VictoriaEndpoints.TracesBase.TrimEnd('/')}"

                [victoria.logs]
                url = "{VictoriaEndpoints.LogsBase.TrimEnd('/')}"

                [victoria.metrics]
                url = "{VictoriaEndpoints.MetricsBase.TrimEnd('/')}"

                [storage]
                provider = "sqlite"
                connection_string = "Data Source={sqlitePath}"

                [telemetry]
                service_name = "tessera-it"
                otlp_endpoint = "http://127.0.0.1:1"
                enable_console_exporter = false
                enable_asp_net_core_instrumentation = false
                enable_http_client_instrumentation = false
                trace_sampling_ratio = 0.0

                [auth]
                default_scheme = "guest"

                [auth.providers.guest]
                enabled = true
                """;
    }

    public static string ResolveHostProjectPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "host", "Tessera.Host", "Tessera.Host.csproj");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            "could not locate Tessera.Host.csproj from " + AppContext.BaseDirectory);
    }
}
