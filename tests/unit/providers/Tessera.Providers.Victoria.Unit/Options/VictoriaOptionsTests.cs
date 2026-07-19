using Tessera.Providers.Victoria.Configuration;
using Xunit;

namespace Tessera.Providers.Victoria.Unit.Options;

/// <summary>
///     Unit tests for <see cref="VictoriaOptions" /> — defaults and
///     initializer-based configuration binding.
/// </summary>
public sealed class VictoriaOptionsTests
{
    /// <summary>
    ///     Default Tenant is "0" and TimeoutMs is 30s — backward-compat with
    ///     MVP-01 single-tenant deployment per HANDOFF §multi-tenancy.md.
    /// </summary>
    [Fact]
    public void Defaults_AreSensible()
    {
        var options = new VictoriaOptions();

        Assert.Equal("0", options.Tenant);
        Assert.Equal(30_000, options.TimeoutMs);
        Assert.Null(options.TracesUrl);
        Assert.Null(options.LogsUrl);
        Assert.Null(options.Auth);
    }

    /// <summary>
    ///     With-initializer (C# 12 primary-ctor with-init pattern) sets
    ///     all properties from the supplied arguments.
    /// </summary>
    [Fact]
    public void Initializer_StoresAllFields()
    {
        var uri = new Uri("http://vt:10428");
        var options = new VictoriaOptions
        {
            TracesUrl = uri,
            Tenant = "5",
            TimeoutMs = 10_000,
        };

        Assert.Equal(uri, options.TracesUrl);
        Assert.Equal("5", options.Tenant);
        Assert.Equal(10_000, options.TimeoutMs);
    }
}