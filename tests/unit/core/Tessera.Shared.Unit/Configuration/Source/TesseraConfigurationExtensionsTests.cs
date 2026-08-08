using Microsoft.Extensions.Configuration;
using Tessera.Shared.Kernel.Configuration.Source;
using Xunit;

namespace Tessera.Shared.Unit.Configuration.Source;

/// <summary>
///     Content-root TOML must bind nested sections (absolute paths + FileProvider).
/// </summary>
public sealed class TesseraConfigurationExtensionsTests
{
    /// <summary>
    ///     Regression: Optional absolute Path without PhysicalFileProvider
    ///     silently skipped the host project tessera.toml (bin BaseDirectory only).
    /// </summary>
    [Fact]
    public void AddTesseraConfiguration_ContentRootToml_BindsNestedVictoriaKeys()
    {
        var dir = Path.Combine(Path.GetTempPath(), "tessera-cfg-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(
                Path.Combine(dir, "tessera.toml"),
                """
                [server]
                host = "127.0.0.1"
                port = 1990

                [victoria.traces]
                url = "https://vtraces.example.test"

                [victoria.logs]
                url = "https://vlogs.example.test"
                """);

            var configuration = new ConfigurationBuilder()
                .AddTesseraConfiguration(dir)
                .Build();

            Assert.Equal("127.0.0.1", configuration["server:host"]);
            Assert.Equal("1990", configuration["server:port"]);
            Assert.Equal("https://vtraces.example.test", configuration["victoria:traces:url"]);
            Assert.Equal("https://vlogs.example.test", configuration["victoria:logs:url"]);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
