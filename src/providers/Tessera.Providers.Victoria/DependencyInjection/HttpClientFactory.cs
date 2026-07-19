namespace Tessera.Providers.Victoria.DependencyInjection;

/// <summary>
///     Helper class for building bare-metal <see cref="HttpClient" /> instances
///     used by the provider's DI registration (MVP-01 ships without the
///     Polly resilience + bearer auth pipeline — those come in MVP-02 when
///     <see cref="Tessera.Shared.Http.RefitExtensions" /> is wired in).
/// </summary>
internal static class HttpClientFactory
{
    /// <summary>
    ///     Create a basic <see cref="HttpClient" /> with the supplied base URL
    ///     and a 30s request timeout.
    /// </summary>
    public static HttpClient CreateFor(string baseUrl)
    {
        var client = new HttpClient { BaseAddress = new Uri(baseUrl) };
        client.Timeout = TimeSpan.FromSeconds(30);
        return client;
    }
}
