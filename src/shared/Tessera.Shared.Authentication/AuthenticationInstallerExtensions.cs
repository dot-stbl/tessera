using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tessera.Shared.Authentication.Admin;
using Tessera.Shared.Kernel.Configuration.Paths;

namespace Tessera.Shared.Authentication;

/// <summary>
///     Composition-root extension for the admin-bearer authentication scheme.
///     Registers the <c>"admin"</c> scheme unconditionally so future
///     <c>[Authorize(Policy = "admin")]</c> annotations work without host
///     changes. When no token source is configured the handler returns
///     <see cref="AuthenticateResult.NoResult()" /> for every request —
///     admin endpoints reject with 401 and the host still starts cleanly
///     (MVP-01 "admin bearer optional" decision).
/// </summary>
public static class AuthenticationInstallerExtensions
{
    /// <summary>
    ///     Convenience keys for the admin token (priority order — first
    ///     non-empty wins). Documented in
    ///     <c>.agents/docs/operations/configure.md</c>.
    ///     <list type="number">
    ///         <item><c>TESSERA:Admin:Token</c> — IConfiguration nested key
    ///             populated by <c>TESSERA__ADMIN__TOKEN</c> env var via
    ///             EnvironmentVariablesConfigurationProvider.</item>
    ///         <item><c>TESSERA_ADMIN_TOKEN</c> — flat config key (TOML or
    ///             env var with single underscore).</item>
    ///     </list>
    /// </summary>
    internal const string NestedConfigKey = "TESSERA:Admin:Token";
    internal const string FlatConfigKey = "TESSERA_ADMIN_TOKEN";

    /// <summary>
    ///     Register the admin-bearer scheme. The expected token is read from
    ///     <see cref="NestedConfigKey" /> first, then
    ///     <see cref="FlatConfigKey" />, then process env. After binding,
    ///     <see cref="SecretReference.Resolve(string?)" /> expands
    ///     <c>env:VAR</c> / <c>file:/path</c> prefixes so the handler sees the
    ///     real token, not the literal reference.
    /// </summary>
    public static AuthenticationBuilder AddTesseraAdminAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<AdminBearerOptions>()
            .PostConfigure(static options => ResolveAdminBearerToken.Apply(options));

        return services
            .AddAuthentication(AdminBearerConstants.SchemeName)
            .AddScheme<AdminBearerOptions, AdminBearerHandler>(
                AdminBearerConstants.SchemeName,
                options =>
                {
                    var raw = configuration[NestedConfigKey]
                        ?? configuration[FlatConfigKey]
                        ?? Environment.GetEnvironmentVariable(FlatConfigKey);
                    options.AdminToken = SecretReference.Resolve(raw);
                });
    }
}

/// <summary>
///     Expands <see cref="AdminBearerOptions.AdminToken" /> through
///     <see cref="SecretReference.Resolve(string?)" />. The PostConfigure
///     hook is the canonical place because <c>IOptionsMonitor</c>
///     resolves at request time, so any later change to the token source
///     (rotation via config reload) flows through PostConfigure at the next
///     composition.
///     <para>
///         <see cref="AdminBearerOptions" /> is a class (records can't
///         inherit from <see cref="AuthenticationSchemeOptions" /> which is
///         itself a class). Mutation via the <c>internal</c> setter is the
///         canonical pattern when ASP.NET options backing isn't a record.
///     </para>
/// </summary>
internal static class ResolveAdminBearerToken
{
    public static void Apply(AdminBearerOptions options)
    {
        if (options.AdminToken is null)
        {
            return;
        }

        var resolved = SecretReference.Resolve(options.AdminToken);
        if (resolved != options.AdminToken)
        {
            options.AdminToken = resolved;
        }
    }
}
