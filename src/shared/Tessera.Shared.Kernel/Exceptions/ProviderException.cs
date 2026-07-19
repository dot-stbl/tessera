namespace Tessera.Shared.Kernel.Exceptions;

/// <summary>
///     Base exception for provider failures (network errors, mapping errors,
///     authentication failures, etc.). Carries a stable error code for clients.
/// </summary>
/// <param name="code">Stable error code (e.g. <c>"provider.network_error"</c>).</param>
/// <param name="message">Human-readable description.</param>
/// <param name="inner">Underlying exception, if any.</param>
public class ProviderException(string code, string message, Exception? inner = null)
        : Exception($"{code}: {message}", inner)
{
    /// <summary>Stable error code.</summary>
    public string Code { get; } = code;
}
