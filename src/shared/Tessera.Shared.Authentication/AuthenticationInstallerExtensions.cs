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
///     changes. When <c>TESSERA_ADMIN_TOKEN</c> is unset the handler returns
///     <see cref="AuthenticateResult.NoResult()" /> for every request —
///     admin endpoints reject with 401 and the host still starts cleanly
///     (MVP-01 "admin bearer optional" decision).
/// </summary>
public static class AuthenticationInstallerExtensions
{
    /// <summary>
    ///     Register the admin-bearer scheme. The expected token is read from
    ///     the <c>TESSERA_ADMIN_TOKEN</c> configuration key (env var
    ///     <c>TESSERA__ADMIN__TOKEN</c>, <c>tessera.toml</c>, or process env).
    ///     After binding, <see cref="SecretReference.Resolve(string?)" />
    ///     expands <c>env:VAR</c> / <c>file:/path</c> prefixes so the
    ///     handler sees the real token, not the literal reference.
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
                    var raw = configuration["TESSERA_ADMIN_TOKEN"]
                        ?? Environment.GetEnvironmentVariable("TESSERA_ADMIN_TOKEN");
                    options.AdminToken = SecretReference.Resolve(raw);
                });
    }
}

/// <summary>
///     Expands <see cref="AdminBearerOptions.AdminToken" /> through
///     <see cref="SecretReference.Resolve(string?)" />. The PostConfigure
///     hook is the canonical place because <c>IOptionsMonitor</c>
///     resolves at request time, so any later change to
///     <c>TESSERA_ADMIN_TOKEN</c> (rotation via config reload) flows through
///     PostConfigure at the next composition.
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
