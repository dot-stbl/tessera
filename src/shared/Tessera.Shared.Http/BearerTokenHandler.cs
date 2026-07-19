namespace Tessera.Shared.Http;

using Microsoft.Extensions.Options;
using Tessera.Shared.Http.Configuration;

/// <summary>
///     DelegatingHandler that attaches <c>Authorization: Bearer &lt;token&gt;</c> header
///     from <see cref="HttpClientAuthOptions" />. Token is read on every request
///     (via <see cref="IOptionsMonitor{T}" />), so changes to the bound config
///     section are picked up without restarting the host.
/// </summary>
public sealed class BearerTokenHandler : DelegatingHandler
{
    private readonly IOptionsMonitor<HttpClientAuthOptions> _options;

    /// <summary>
    ///     Constructs the handler. The options monitor is captured by reference;
    ///     token rotation is reflected on the next outbound request without
    ///     rebuilding the handler.
    /// </summary>
    public BearerTokenHandler(IOptionsMonitor<HttpClientAuthOptions> options)
    {
        _options = options;
    }

    /// <summary>
    ///     Attaches <c>Authorization: Bearer</c> header (when a token is configured)
    ///     before delegating to the inner handler pipeline.
    /// </summary>
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var token = _options.CurrentValue.AuthToken;
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        return base.SendAsync(request, cancellationToken);
    }
}