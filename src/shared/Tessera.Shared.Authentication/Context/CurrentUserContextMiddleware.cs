using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

namespace Tessera.Shared.Authentication.Context;

/// <summary>
///     Middleware that materializes the current request's principal
///     into the scoped <see cref="CurrentUserContext" />. Runs after
///     <c>UseAuthentication</c> so <see cref="HttpContext.User" /> is
///     already populated by the chosen scheme; we read it once and
///     store it for the rest of the pipeline.
///     <para>
///         Per ADR-0001 §5 — handlers depend on
///         <see cref="ICurrentUserContext" /> (injected, testable),
///         not on <c>HttpContext.User</c> scattered through feature
///         code.
///     </para>
/// </summary>
public sealed class CurrentUserContextMiddleware(RequestDelegate next)
{
    /// <summary>Middleware entry point.</summary>
    public async Task InvokeAsync(
        HttpContext context,
        IAuthenticationSchemeProvider schemeProvider,
        IAuthenticationService authentication,
        CurrentUserContext current)
    {
        var scheme = await schemeProvider.GetDefaultAuthenticateSchemeAsync();

        if (scheme is not null)
        {
            var result = await authentication.AuthenticateAsync(context, scheme.Name);
            if (result.Principal is not null)
            {
                current.Initialize(result.Principal);
            }
        }

        await next(context);
    }
}
