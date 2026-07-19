namespace Tessera.Shared.Kernel.Exceptions;

/// <summary>
/// Provider reported the requested resource does not exist
/// (e.g. <c>trace_id</c> not found, service not registered).
/// </summary>
/// <param name="code">Stable error code (e.g. <c>"trace.not_found"</c>).</param>
/// <param name="message">Human-readable description.</param>
/// <param name="inner">Underlying exception, if any.</param>
public sealed class ProviderNotFoundException(string code, string message, Exception? inner = null)
    : ProviderException(code, message, inner);