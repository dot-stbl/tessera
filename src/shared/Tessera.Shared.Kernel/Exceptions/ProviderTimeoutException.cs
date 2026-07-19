namespace Tessera.Shared.Kernel.Exceptions;

/// <summary>
///     Provider request exceeded its timeout budget.
/// </summary>
/// <param name="code">Stable error code (e.g. <c>"provider.timeout"</c>).</param>
/// <param name="message">Human-readable description.</param>
/// <param name="inner">Underlying exception, if any.</param>
public sealed class ProviderTimeoutException(string code, string message, Exception? inner = null)
        : ProviderException(code, message, inner);
