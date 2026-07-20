using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Tessera.Stack.Testing.Fixtures;

/// <summary>
///     <c>WebApplicationFactory&lt;TEntryPoint&gt;</c> specialised to
///     stand up <c>Tessera.Host</c> with its configuration pointed at
///     a running compose stack (or a stand-alone Victoria cluster).
///     Tests pull an <see cref="System.Net.Http.HttpClient" /> from
///     the inherited <c>CreateClient()</c> method and hit the public
///     <c>/api/v1/*</c> surface directly — no Refit / network mocking.
/// </summary>
/// <remarks>
///     <para>
///         The host uses the TOML config layer
///         (<c>AddTesseraConfiguration()</c>) which respects
///         <c>TESSERA_CONFIG</c> as the primary config-file lookup.
///         The factory sets <see cref="TesseraConfigurationFileOverride" />
///         to point the loader at a transient file in
///         <see cref="System.IO.Path.GetTempPath" /> so the test never
///         depends on what's checked into the repository or running
///         in <c>/etc/tessera</c>.
///     </para>
///     <para>
///         Victoria and admin URLs are also injected as configuration
///         overrides so the host reads from the compose-published
///         localhost ports (<c>8428</c>/<c>9428</c>/<c>10428</c>) rather
///         than anything baked into a TOML file.
///     </para>
/// </remarks>
/// <remarks>Constructor overload for tests that override the stack URLs.</remarks>
public sealed class TesseraStackFixture(Uri tracesUrl, Uri logsUrl, Uri metricsUrl) : WebApplicationFactory<Tessera.Host.Program>
{
    private readonly Uri _tracesUrl = tracesUrl;
    private readonly Uri _logsUrl = logsUrl;
    private readonly Uri _metricsUrl = metricsUrl;

    /// <summary>
    ///     Constructs the fixture against the standard compose stack
    ///     service URLs (<c>localhost:8428</c>, <c>localhost:9428</c>,
    ///     <c>localhost:10428</c>). Use the explicit-URL overload when
    ///     the test runs against a different cluster.
    /// </summary>
    public TesseraStackFixture()
        : this(
            tracesUrl: new Uri("http://localhost:10428"),
            logsUrl: new Uri("http://localhost:9428"),
            metricsUrl: new Uri("http://localhost:8428"))
    {
    }

    /// <summary>Path the host's TOML loader reads from. Useful for test diagnostics.</summary>
    public string TesseraConfigurationFileOverride { get; } = Path.Combine(
            Path.GetTempPath(),
            $"tessera-integration-{Guid.NewGuid():N}.toml");

    /// <summary>
    ///     Overrides the inherited <c>ConfigureWebHost</c> hook to bind
    ///     the host to the supplied Victoria URLs and to point the TOML
    ///     loader at the transient config file the constructor wrote.
    /// </summary>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        WriteDevConfig(TesseraConfigurationFileOverride, _tracesUrl, _logsUrl, _metricsUrl);
        Environment.SetEnvironmentVariable("TESSERA_CONFIG", TesseraConfigurationFileOverride);

        builder.UseEnvironment("Testing")
            .ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["victoria:traces:url"] = _tracesUrl.ToString(),
                ["victoria:logs:url"] = _logsUrl.ToString(),
                ["victoria:metrics:url"] = _metricsUrl.ToString(),
                ["server:port"] = "0",
            });
        });
        builder.ConfigureLogging(static logging => logging.AddFilter("Microsoft", LogLevel.Warning));
    }

    /// <summary>
    ///     Removes the transient TOML file. The compose stack is owned
    ///     by <see cref="DockerComposeFixture" />,
    ///     not by this factory; tests that share the compose via
    ///     <c>ICollectionFixture</c> dispose compose separately.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && File.Exists(TesseraConfigurationFileOverride))
        {
            File.Delete(TesseraConfigurationFileOverride);
        }
    }

    private static void WriteDevConfig(string path, Uri tracesUrl, Uri logsUrl, Uri metricsUrl)
    {
        var body = $$"""
                     [server]
                     host = "127.0.0.1"
                     port = 0

                     [victoria]
                     tenant = "0"
                     timeout_ms = 5000

                     [victoria.traces]
                     url = "{{tracesUrl}}"
                     token = ""

                     [victoria.logs]
                     url = "{{logsUrl}}"
                     token = ""

                     [victoria.metrics]
                     url = "{{metricsUrl}}"
                     token = ""

                     [auth]
                     """;
        File.WriteAllText(path, body);
    }
}
