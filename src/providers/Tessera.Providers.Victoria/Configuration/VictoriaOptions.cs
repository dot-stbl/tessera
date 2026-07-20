using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;
using Tessera.Shared.Http.Configuration;

namespace Tessera.Providers.Victoria.Configuration;

/// <summary>
///     Victoria stack backend connection options. Bound from <c>tessera.toml</c>
///     via <see cref="IOptions{TOptions}" /> at composition root. The
///     <c>[victoria.traces]</c> / <c>[victoria.logs]</c> / <c>[victoria.metrics]</c>
///     nested sections bind to <see cref="Traces" /> / <see cref="Logs" /> /
///     <see cref="Metrics" /> respectively — see
///     <c>.agents/docs/architecture/config-format.md</c>.
/// </summary>
public sealed class VictoriaOptions
{
    /// <summary>VictoriaTraces (vtselect) backend options.</summary>
    [Required]
    public required VictoriaBackendOptions Traces { get; init; }

    /// <summary>VictoriaLogs (vlselect) backend options.</summary>
    [Required]
    public required VictoriaBackendOptions Logs { get; init; }

    /// <summary>
    ///     VictoriaMetrics (vmselect) backend options. Optional — MVP-01 has
    ///     no IMetricsProvider impl, but the section is parsed so a future
    ///     release can wire metrics without a TOML schema change.
    /// </summary>
    public VictoriaBackendOptions? Metrics { get; init; }

    /// <summary>Single-tenant path prefix, e.g. <c>"0"</c>.</summary>
    [Required]
    public string Tenant { get; init; } = "0";

    /// <summary>Request timeout (milliseconds). Default 30_000 (30s).</summary>
    [Range(1, 600_000)]
    public int TimeoutMs { get; init; } = 30_000;

    /// <summary>Shared HTTP auth options (bearer token, etc.).</summary>
    public HttpClientAuthOptions? Auth { get; init; }
}

/// <summary>
///     Per-backend connection options shared by traces, logs, and metrics
///     sections. <see cref="Token" /> is resolved through
///     <c>SecretReference.Resolve</c> in PostConfigure (see Phase 2c) so the
///     raw value (e.g. <c>"env:TESSERA_VICTORIA_TOKEN"</c>) gets expanded
///     to the actual token before the provider reads it.
/// </summary>
public sealed class VictoriaBackendOptions
{
    /// <summary>Backend base URL (e.g. <c>"http://vt:10428"</c> for traces).</summary>
    [Required]
    [Url]
    public required Uri Url { get; init; }

    /// <summary>
    ///     Inline secret reference. Recognised prefixes per
    ///     <c>.agents/docs/architecture/config-format.md</c>: <c>env:VAR</c>
    ///     and <c>file:/path</c>. Resolved at startup; literal otherwise.
    /// </summary>
    public string? Token { get; init; }
}
