using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tessera.Shared.Authentication.Core;
using Tessera.Shared.Authentication.Providers.Admin;
using Tessera.Shared.Authentication.Providers.Guest;
using Tessera.Shared.Authentication.Providers.Ldap;

namespace Tessera.Shared.Authentication;

/// <summary>
///     Tessera multi-provider authentication framework. Hosts register
///     one or more <see cref="IAuthProvider" /> schemes via
///     <see cref="AddTesseraAuthentication" />. Default scheme is taken
///     from <see cref="TesseraAuthenticationOptions.DefaultScheme" />
///     (<c>"guest"</c> by default so anonymous reads work without
///     explicit <c>[Authorize]</c>; can be overridden via TOML).
///     <para>
///         Each provider registered here becomes available as a
///         <see cref="Microsoft.AspNetCore.Authentication.AuthenticationScheme" />;
///         authorization policies can target it via
///         <c>services.AddAuthorization(options =&gt; options.AddPolicy("admin",
///         p =&gt; p.AddAuthenticationSchemes(AdminBearerAuthProvider.NameConst).RequireRole("admin")))</c>
///         (with similar AddAuthenticationSchemes for LdapAuthProvider.NameConst).
///     </para>
///     <para>
///         Per ADR-0001 Decision 1 — multi-provider auth framework. MVP-01
///         ships Guest + AdminBearer; LDAP (4b) and Keycloak (4c) plug in
///         via the same <see cref="IAuthProvider" /> shape.
///     </para>
/// </summary>
public static class AuthenticationInstallerExtensions
{
    /// <summary>
    ///     Wire Tessera's multi-provider auth framework. Always registers
    ///     the Guest scheme (anonymous-read default) — independent of
    ///     <c>[auth.providers.guest]/enabled</c>. Conditionally
    ///     registers AdminBearer when
    ///     <c>[auth.providers.admin-bearer]/enabled = true</c> and LDAP
    ///     when <c>[auth.providers.ldap]/enabled = true</c>.
    ///     Keycloak scheme will be added when Phase 4c lands.
    /// </summary>
    /// <param name="services">Host service collection.</param>
    /// <param name="configuration">Host configuration root.</param>
    /// <returns>The <see cref="AuthenticationBuilder" /> for chained
    /// scheme registration in the host.</returns>
    public static AuthenticationBuilder AddTesseraAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        _ = services
            .AddOptions<TesseraAuthenticationOptions>()
            .Bind(configuration.GetSection(TesseraAuthenticationOptions.SectionName));

        var defaultScheme = configuration
            .GetSection(TesseraAuthenticationOptions.SectionName)["default_scheme"] ?? "guest";

        var builder = services
            .AddAuthentication(options => options.DefaultScheme = defaultScheme)
            .AddScheme<GuestAuthOptions, GuestAuthHandler>(
                GuestAuthProvider.NameConst,
                _ => { });

        if (configuration
            .GetSection($"{TesseraAuthenticationOptions.SectionName}.{TesseraAuthenticationOptions.ProvidersSectionName}.admin-bearer")
            .GetValue<bool>("enabled"))
        {
            builder = builder.AddScheme<AdminBearerOptions, AdminBearerHandler>(
                AdminBearerAuthProvider.NameConst,
                _ => { });

            _ = services
                .AddOptions<AdminBearerOptions>()
                .Bind(configuration.GetSection($"{TesseraAuthenticationOptions.SectionName}.{TesseraAuthenticationOptions.ProvidersSectionName}.admin-bearer"));

            services.AddOptions<AdminBearerOptions>()
                .PostConfigure(ResolveAdminBearerToken.Apply);
        }

        if (configuration
            .GetSection($"{TesseraAuthenticationOptions.SectionName}.{TesseraAuthenticationOptions.ProvidersSectionName}.ldap")
            .GetValue<bool>("enabled"))
        {
            builder = builder.AddScheme<LdapAuthOptions, LdapAuthHandler>(
                LdapAuthProvider.NameConst,
                _ => { });

            _ = services
                .AddOptions<LdapAuthOptions>()
                .Bind(configuration.GetSection(LdapAuthOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddOptions<LdapAuthOptions>()
                .PostConfigure(ResolveLdapCredentials.Apply);
        }

        services.AddSingleton<IAuthProvider, GuestAuthProvider>();
        services.AddSingleton<IAuthProvider, AdminBearerAuthProvider>();
        services.AddSingleton<IAuthProvider, LdapAuthProvider>();

        return builder;
    }
}
