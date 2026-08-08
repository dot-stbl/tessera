using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Tessera.Shared.Authentication.Context;

namespace Tessera.Shared.Authentication;

/// <summary>
///     DI / pipeline extensions for
///     <see cref="Tessera.Shared.Authentication.Context.ICurrentUserContext" />.
///     Registered as part of the multi-provider auth framework so host
///     and shared modules depend on a single injection site.
/// </summary>
public static class CurrentUserContextServiceCollectionExtensions
{
    /// <summary>
    ///     Register <see cref="ICurrentUserContext" /> as a scoped
    ///     service. The actual <see cref="CurrentUserContext" /> is
    ///     instantiated by the DI container per-request; the
    ///     middleware populates it from the chosen scheme.
    /// </summary>
    public static IServiceCollection AddTesseraCurrentUserContext(
        this IServiceCollection services)
    {
        services.AddScoped<CurrentUserContext>();
        services.AddScoped<ICurrentUserContext>(
            static sp => sp.GetRequiredService<CurrentUserContext>());

        return services;
    }

    /// <summary>
    ///     Wire the per-request materialization middleware. Call this
    ///     AFTER <c>app.UseAuthentication()</c> so the default
    ///     scheme has populated <c>HttpContext.User</c>.
    ///     <para>
    ///         Pipeline position (host):
    ///         <c>UseRouting → UseAuthentication → UseTesseraCurrentUserContext → UseAuthorization</c>.
    ///     </para>
    /// </summary>
    public static IApplicationBuilder UseTesseraCurrentUserContext(
        this IApplicationBuilder application)
    {
        return application.UseMiddleware<CurrentUserContextMiddleware>();
    }
}
