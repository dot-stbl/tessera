using System.ComponentModel.DataAnnotations;

namespace Tessera.Shared.Kernel.Configuration;

/// <summary>
///     HTTP server bind settings. Bound from the <c>[server]</c> TOML table by
///     <see cref="TesseraConfigurationExtensions.AddTesseraConfiguration(Microsoft.Extensions.Configuration.IConfigurationBuilder)" />.
///     <see cref="Port" /> is constrained to the Tessera-reserved range
///     (1990–2120, per <c>.agents/rules/coding/project-ports.md</c>) so a typo
///     in <c>tessera.toml</c> fails startup, not at first request.
/// </summary>
public sealed class ServerOptions
{
    /// <summary>Configuration section name in <c>tessera.toml</c>.</summary>
    public const string SectionName = "server";

    /// <summary>Interface to bind on. Default <c>"0.0.0.0"</c> listens on all interfaces.</summary>
    public string Host { get; init; } = "0.0.0.0";

    /// <summary>TCP port. Tessera-reserved MVP range is 1990–2120 (port 1990 = backend).</summary>
    [Range(1990, 2120)]
    public int Port { get; init; } = 1990;
}