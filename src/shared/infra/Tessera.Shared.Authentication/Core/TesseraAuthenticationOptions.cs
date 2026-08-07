namespace Tessera.Shared.Authentication.Core;

/// <summary>
///     Top-level <c>[auth]</c> configuration section for Tessera.
///     Bound from <c>tessera.toml</c> at composition root. Per ADR-0001
///     Decision 1 — multi-provider auth framework.
///     <para>
///         <c>default_scheme</c> selects the auth scheme used by
///         <c>[Authorize]</c> with no policy argument. Default <c>"guest"</c>
///         so anonymous reads work without explicit <c>[Authorize]</c>.
///     </para>
///     <para>
///         <c>providers</c> is a free-form dictionary of <c>{name: enabled}</c>
///         — the multi-provider framework reads each
///         <c>[auth.providers.&lt;name&gt;]</c> section via its own
///         <see cref="IAuthProvider" /> implementation.
///     </para>
/// </summary>
public sealed class TesseraAuthenticationOptions
{
    /// <summary>Configuration section name in <c>tessera.toml</c>.</summary>
    public const string SectionName = "auth";

    /// <summary>Sub-section name for individual provider settings.</summary>
    public const string ProvidersSectionName = "providers";

    /// <summary>
    ///     Default authentication scheme when no <c>[Authorize(Policy=...)]</c>
    ///     matches. <c>"guest"</c> for MVP-01 (anonymous read OK); will
    ///     typically be <c>"guest"</c> or <c>"bearer"</c> in production.
    /// </summary>
    public string DefaultScheme { get; init; } = "guest";

    /// <summary>
    ///     Per-provider enabled flags. Key = provider name (matches
    ///     <see cref="IAuthProvider.Name" />), value = whether the
    ///     configuration section is enabled. Empty dictionary = no
    ///     providers explicitly enabled (Guest is always implicitly
    ///     available as the default scheme).
    /// </summary>
    public Dictionary<string, bool> Providers { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
