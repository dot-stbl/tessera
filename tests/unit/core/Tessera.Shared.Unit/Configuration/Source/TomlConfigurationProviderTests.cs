using Microsoft.Extensions.Configuration;
using Tessera.Shared.Kernel.Configuration.Source;
using Tessera.Shared.Kernel.Exceptions;
using Xunit;

namespace Tessera.Shared.Kernel.Tests.Configuration.Source;

/// <summary>
///     Regression tests for <see cref="TomlConfigurationProvider" />:
///     empty input, valid TOML flattening, malformed-TOML boundary translation.
/// </summary>
public sealed class TomlConfigurationProviderTests
{
    /// <summary>
    ///     Builds a provider from in-memory TOML text and exposes its data
    ///     via the standard <see cref="IConfigurationProvider.TryGet" />
    ///     surface — which avoids going through
    ///     <see cref="ConfigurationRoot" /> (whose constructor would call
    ///     the file-based <c>Load()</c> on the provider and overwrite the
    ///     already-parsed in-memory data when the file is missing).
    /// </summary>
    private static TomlConfigurationProvider BuildProvider(string tomlText)
    {
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(tomlText));
        var source = new TomlConfigurationSource { Path = "tessera.toml" };
        var provider = new TomlConfigurationProvider(source);
        provider.Load(stream);
        return provider;
    }

    private static string? Get(TomlConfigurationProvider provider, string key)
    {
        provider.TryGet(key, out var value);
        return value;
    }

    /// <summary>
    ///     Empty input (whitespace-only stream) is the documented
    ///     short-circuit — no parse attempt, empty dictionary.
    /// </summary>
    [Fact]
    public void LoadStream_EmptyText_ProducesEmptyData()
    {
        var provider = BuildProvider(string.Empty);

        Assert.Null(Get(provider, "anything"));
    }

    /// <summary>
    ///     Nested TOML tables flatten to <c>section:key</c> keys with
    ///     the case-insensitive dictionary the host's <c>IConfiguration</c>
    ///     pipeline expects.
    /// </summary>
    [Fact]
    public void LoadStream_ValidToml_FlattenPopulatesData()
    {
        const string toml = """
                             [server]
                             host = "0.0.0.0"
                             port = 1990

                             [victoria.traces]
                             url = "http://vt:10428"
                             token = ""
                             """;
        var provider = BuildProvider(toml);

        Assert.Equal("0.0.0.0", Get(provider, "server:host"));
        Assert.Equal("1990", Get(provider, "server:port"));
        Assert.Equal("http://vt:10428", Get(provider, "victoria:traces:url"));
        Assert.Equal("", Get(provider, "victoria:traces:token"));
    }

    /// <summary>
    ///     Regression test for the <see cref="TomlConfigurationProvider" />
    ///     parse-error boundary translation. A mismatched bracket is the
    ///     smallest possible typo; without the fix the host crashes with
    ///     a raw <c>TomlException</c> the global handler cannot map.
    /// </summary>
    [Fact]
    public void LoadStream_MalformedToml_ThrowsProviderExceptionWithConfigParseError()
    {
        // Mismatched bracket — Tomlyn rejects with line/column info.
        const string malformed = """
                                [server
                                port = 1990
                                """;
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(malformed));
        var provider = new TomlConfigurationProvider(
            new TomlConfigurationSource { Path = "tessera.toml" });

        var exception = Assert.Throws<ProviderException>(() => provider.Load(stream));

        Assert.Equal("config.parse_error", exception.Code);
        Assert.NotNull(exception.InnerException);
    }
}
