using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tessera.Shared.Authentication.Admin;

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
    ///     <c>TESSERA__ADMIN__TOKEN</c> or <c>tessera.toml</c>); if neither is
    ///     set, the handler treats the scheme as disabled but the host starts
    ///     cleanly.
    /// </summary>
    public static AuthenticationBuilder AddTesseraAdminAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        return services
            .AddAuthentication(AdminBearerConstants.SchemeName)
            .AddScheme<AdminBearerOptions, AdminBearerHandler>(
                AdminBearerConstants.SchemeName,
                options =>
                {
                    options.AdminToken = configuration["TESSERA_ADMIN_TOKEN"]
                        ?? Environment.GetEnvironmentVariable("TESSERA_ADMIN_TOKEN");
                });
    }
}
