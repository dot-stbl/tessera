using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;
using Tessera.Shared.Http.Configuration;

namespace Tessera.Providers.Victoria.Configuration;

/// <summary>
///     Victoria stack backend connection options. Bound from <c>tessera.toml</c>
///     via <see cref="IOptions{TOptions}" /> at composition root.
/// </summary>
public sealed class VictoriaOptions
{
    /// <summary>VictoriaTraces (vtselect) base URL, e.g. <c>"http://vt:10428"</c>.</summary>
    [Required]
    [Url]
    public Uri? TracesUrl { get; init; }

    /// <summary>VictoriaLogs (vlselect) base URL, e.g. <c>"http://vl:9428"</c>.</summary>
    [Required]
    [Url]
    public Uri? LogsUrl { get; init; }

    /// <summary>Single-tenant path prefix, e.g. <c>"0"</c>.</summary>
    [Required]
    public string Tenant { get; init; } = "0";

    /// <summary>Request timeout (milliseconds). Default 30_000 (30s).</summary>
    [Range(1, 600_000)]
    public int TimeoutMs { get; init; } = 30_000;

    /// <summary>Shared HTTP auth options (bearer token, etc.).</summary>
    public HttpClientAuthOptions? Auth { get; init; }
}