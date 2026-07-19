using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using Tessera.Shared.Http.Configuration;

namespace Tessera.Shared.Http;

/// <summary>
///     DelegatingHandler that attaches <c>Authorization: Bearer &lt;token&gt;</c> header
///     from <see cref="HttpClientAuthOptions" />. Token is read on every request
///     (via <see cref="IOptionsMonitor{T}" />), so changes to the bound config
///     section are picked up without restarting the host.
/// </summary>
/// <remarks>
///     Constructs the handler. The options monitor is captured by reference;
///     token rotation is reflected on the next outbound request without
///     rebuilding the handler.
/// </remarks>
public sealed class BearerTokenHandler(IOptionsMonitor<HttpClientAuthOptions> options) : DelegatingHandler
{
    /// <summary>
    ///     Attaches <c>Authorization: Bearer</c> header (when a token is configured)
    ///     before delegating to the inner handler pipeline.
    /// </summary>
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var token = options.CurrentValue.AuthToken;

        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
