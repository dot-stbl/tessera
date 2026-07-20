using Microsoft.Extensions.DependencyInjection;
using Tessera.Providers.Victoria.Configuration;
using Tessera.Shared.Http.Configuration;
using Tessera.Shared.Kernel.Configuration.Paths;

namespace Tessera.Providers.Victoria.DependencyInjection;

/// <summary>
///     Wires <see cref="SecretReference.Resolve(string?)" /> into PostConfigure
///     for every <see cref="VictoriaOptions" /> secret field. Without this,
///     raw values like <c>"env:TESSERA_VICTORIA_TOKEN"</c> would land in the
///     HTTP client header verbatim — see STATE.md "Open questions" /
///     ADR-0001 Decision 5 for context.
/// </summary>
/// <remarks>
///     <para>
///         Each PostConfigure uses <c>with</c>-expressions on the
///         nested record types. Records require <c>init</c>-only properties;
///         that's why <see cref="VictoriaBackendOptions" /> and
///         <see cref="HttpClientAuthOptions" /> are declared as records
///         (Phase 2b refactor).
///     </para>
///     <para>
///         Operations are idempotent: <see cref="SecretReference.Resolve" />
///         passes through literal values unchanged, so a token that is
///         already the resolved form (e.g. set directly in TOML) is not
///         touched.
///     </para>
/// </remarks>
public static class VictoriaSecretsConfiguration
{
    /// <summary>
    ///     Resolve <c>env:VAR</c> / <c>file:/path</c> on every secret field
    ///     in <see cref="VictoriaOptions" />. Call after the
    ///     <c>AddOptions&lt;T&gt;().Bind(...)</c> chain so
    ///     <c>ValidateOnStart</c> sees the resolved values.
    /// </summary>
    public static IServiceCollection ResolveVictoriaSecrets(this IServiceCollection services)
    {
        services.AddOptions<VictoriaOptions>()
            .PostConfigure(static options => ResolveAllSecrets.Apply(options));
        return services;
    }
}

/// <summary>
///     Walks <see cref="VictoriaOptions.Traces" />,
///     <see cref="VictoriaOptions.Logs" />, <see cref="VictoriaOptions.Metrics" />,
///     and <see cref="VictoriaOptions.Auth" />. For each, the raw
///     <c>Token</c> / <c>AuthToken</c> value (if non-null) is replaced with
///     the result of <see cref="SecretReference.Resolve(string?)" />.
/// </summary>
internal static class ResolveAllSecrets
{
    public static VictoriaOptions Apply(VictoriaOptions options)
    {
        var traces = ResolveBackend(options.Traces);
        var logs = ResolveBackend(options.Logs);
        var metrics = options.Metrics is { } m ? ResolveBackend(m) : null;

        HttpClientAuthOptions? auth = null;
        if (options.Auth is { } authRecord && authRecord.AuthToken is { } rawAuthToken)
        {
            var resolvedAuth = SecretReference.Resolve(rawAuthToken);
            auth = resolvedAuth == rawAuthToken
                ? authRecord
                : authRecord with { AuthToken = resolvedAuth };
        }

        return options with
        {
            Traces = traces,
            Logs = logs,
            Metrics = metrics,
            Auth = auth,
        };
    }

    private static VictoriaBackendOptions ResolveBackend(VictoriaBackendOptions backend)
    {
        if (backend.Token is null)
        {
            return backend;
        }

        var resolved = SecretReference.Resolve(backend.Token);
        return resolved == backend.Token
            ? backend
            : backend with { Token = resolved };
    }
}
