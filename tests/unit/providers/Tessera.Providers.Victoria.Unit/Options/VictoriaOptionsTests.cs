using Tessera.Providers.Victoria.Configuration;
using Xunit;

namespace Tessera.Providers.Victoria.Unit.Options;

/// <summary>
///     Unit tests for <see cref="VictoriaOptions" /> — defaults and
///     initializer-based configuration binding. With the nested
///     <see cref="VictoriaBackendOptions" /> shape, <c>Traces</c> and
///     <c>Logs</c> are <c>required</c>; tests must pass the nested
///     option when constructing directly.
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
        var traces = new VictoriaBackendOptions { Url = new Uri("http://vt:10428") };
        var logs = new VictoriaBackendOptions { Url = new Uri("http://vl:9428") };
        var options = new VictoriaOptions { Traces = traces, Logs = logs };

        Assert.Equal("0", options.Tenant);
        Assert.Equal(30_000, options.TimeoutMs);
        Assert.Same(traces, options.Traces);
        Assert.Same(logs, options.Logs);
        Assert.Null(options.Auth);
        Assert.Null(options.Metrics);
    }

    /// <summary>
    ///     With-initializer (C# 12 primary-ctor with-init pattern) sets
    ///     all properties from the supplied arguments, including the
    ///     nested Traces/Logs sub-options.
    /// </summary>
    [Fact]
    public void Initializer_StoresAllFields()
    {
        var traces = new VictoriaBackendOptions
        {
            Url = new Uri("http://vt:10428"),
            Token = "env:TESSERA_VICTORIA_TOKEN",
        };
        var logs = new VictoriaBackendOptions
        {
            Url = new Uri("http://vl:9428"),
            Token = "env:TESSERA_VICTORIA_TOKEN",
        };
        var options = new VictoriaOptions
        {
            Traces = traces,
            Logs = logs,
            Tenant = "5",
            TimeoutMs = 10_000,
        };

        Assert.Same(traces, options.Traces);
        Assert.Same(logs, options.Logs);
        Assert.Equal("5", options.Tenant);
        Assert.Equal(10_000, options.TimeoutMs);
        Assert.Equal("env:TESSERA_VICTORIA_TOKEN", options.Traces.Token);
    }
}
