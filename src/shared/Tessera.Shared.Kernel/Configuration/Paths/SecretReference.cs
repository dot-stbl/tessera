namespace Tessera.Shared.Kernel.Configuration.Paths;

/// <summary>
///     Parses and resolves inline secret references that appear in TOML config
///     values. Recognized prefixes per
///     <c>.agents/docs/architecture/config-format.md</c>:
///     <list type="bullet">
///         <item><c>env:VAR_NAME</c>: reads the named environment variable at resolution time. Used for tokens and other secrets that must never be committed to the config file.</item>
///         <item><c>file:/path</c>: reads the file at the given absolute path, trims whitespace. Used for sidecar-mounted secret files such as <c>/etc/tessera/secrets/admin-token</c> that the deployment orchestrator manages separately from the TOML config.</item>
///         <item>any other value: treated as a literal, used as-is (dev only).</item>
///     </list>
/// </summary>
public static class SecretReference
{
    /// <summary>Prefix marking an environment-variable reference, e.g. <c>env:TESSERA_ADMIN_TOKEN</c>.</summary>
    public const string EnvPrefix = "env:";

    /// <summary>Prefix marking a file-path reference, e.g. <c>file:/etc/tessera/secrets/admin-token</c>.</summary>
    public const string FilePrefix = "file:";

    /// <summary>
    ///     Resolve <paramref name="raw" /> to its effective value. Returns
    ///     <c>null</c> only when the input is empty or whitespace; literal
    ///     values pass through unchanged; env-var misses return <c>null</c> so
    ///     callers can decide how to fail (startup validation vs runtime 5xx).
    /// </summary>
    public static string? Resolve(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return raw;
        }

        if (raw.StartsWith(EnvPrefix, StringComparison.Ordinal))
        {
            return Environment.GetEnvironmentVariable(raw[EnvPrefix.Length..]);
        }

        if (raw.StartsWith(FilePrefix, StringComparison.Ordinal))
        {
            var path = raw[FilePrefix.Length..];
            return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
        }

        return raw;
    }
}
