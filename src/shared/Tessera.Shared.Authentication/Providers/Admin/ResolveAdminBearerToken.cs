using Microsoft.Extensions.Options;
using Tessera.Shared.Kernel.Configuration.Paths;

namespace Tessera.Shared.Authentication.Providers.Admin;

/// <summary>
///     PostConfigure hook for <see cref="AdminBearerOptions" />. Resolves the
///     <c>AdminToken</c> from <c>TESSERA_ADMIN_TOKEN</c> or the value bound
///     from <c>[auth.providers.admin-bearer]/token</c>. The bound value can
///     be either a literal token or a <see cref="SecretReference" />
///     (<c>"env:VAR"</c>, <c>"file:/path"</c>). Implemented as a file-static
///     helper so <see cref="IPostConfigureOptions{TOptions}" /> classes don't
///     multiply (code-shape.md §9 ban).
///     <para>
///         Precedence: explicit configuration value (literal or
///         <see cref="SecretReference" />) wins; if absent, the
///         <c>TESSERA_ADMIN_TOKEN</c> environment variable is consulted as
///         a fallback. The handler itself returns
///         <see cref="Microsoft.AspNetCore.Authentication.AuthenticateResult.NoResult" />
///         when the value remains empty — admin endpoints then 401 cleanly,
///         allowing the host to start with admin disabled
///         (MVP-01 "admin bearer optional").
///     </para>
/// </summary>
internal static class ResolveAdminBearerToken
{
    /// <summary>
    ///     The <see cref="IConfigureOptions{TOptions}" /> delegate wired
    ///     by <c>AddOptions&lt;AdminBearerOptions&gt;().PostConfigure(...)</c>.
    /// </summary>
    public static void Apply(AdminBearerOptions options)
    {
        var resolved = SecretReference.Resolve(options.AdminToken);
        if (!string.IsNullOrEmpty(resolved))
        {
            options.AdminToken = resolved;
            return;
        }

        var environment = Environment.GetEnvironmentVariable("TESSERA_ADMIN_TOKEN");
        if (!string.IsNullOrEmpty(environment))
        {
            options.AdminToken = environment;
        }
    }

    /// <summary>
    ///     Long-form resolution when the bound value is a
    ///     <see cref="SecretReference" />. Splits on the first colon; literal
    ///     values pass through unchanged. Returns <c>null</c> when the source
    ///     is absent (caller falls back to environment). Currently not wired
    ///     in MVP-01 — kept as a visible extension point for Phase 4d
    ///     (<c>ICurrentUserContext</c>) once a real <see cref="SecretReference" />
    ///     binding lands in <c>[auth.providers.admin-bearer].token</c>.
    /// </summary>
    public static string? ResolveSecretReference(string? rawValue)
    {
        return SecretReference.Resolve(rawValue);
    }
}
