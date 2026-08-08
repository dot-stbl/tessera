using Microsoft.Extensions.DependencyInjection;
using Tessera.Providers.Victoria.Configuration;
using Tessera.Shared.Kernel.Configuration.Paths;

namespace Tessera.Providers.Victoria.DependencyInjection;

/// <summary>
///     Wires <see cref="SecretReference.Resolve(string?)" /> into PostConfigure
///     for every <see cref="VictoriaOptions" /> secret field. Without this,
///     raw values like <c>"env:TESSERA_VICTORIA_TOKEN"</c> would land in the
///     HTTP client header verbatim.
/// </summary>
/// <remarks>
///     Mutates nested tokens in place. <c>PostConfigure</c> is an
///     <c>Action</c> — a <c>with</c>-expression return value is discarded and
///     never applied to the options instance.
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
            .PostConfigure(static options => VictoriaSecretsResolver.ApplyInPlace(options));
        return services;
    }
}

/// <summary>
///     Walks Traces / Logs / Metrics / Auth and resolves secret references
///     in place. Null backends are skipped — ValidateOnStart still fails
///     missing required sections.
/// </summary>
internal static class VictoriaSecretsResolver
{
    public static void ApplyInPlace(VictoriaOptions options)
    {
        if (options.Traces is { } traces)
        {
            ResolveBackendInPlace(traces);
        }

        if (options.Logs is { } logs)
        {
            ResolveBackendInPlace(logs);
        }

        if (options.Metrics is { } metrics)
        {
            ResolveBackendInPlace(metrics);
        }

        if (options.Auth is { AuthToken: { } rawAuthToken } auth)
        {
            auth.AuthToken = SecretReference.Resolve(rawAuthToken);
        }
    }

    public static void ResolveBackendInPlace(VictoriaBackendOptions backend)
    {
        if (backend.Token is null)
        {
            return;
        }

        backend.Token = SecretReference.Resolve(backend.Token);
    }
}
