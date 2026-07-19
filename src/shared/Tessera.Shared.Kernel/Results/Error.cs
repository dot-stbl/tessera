namespace Tessera.Shared.Kernel.Results;

/// <summary>
/// Structured error. <see cref="Code"/> is a stable machine-readable identifier
/// (e.g. <c>"trace.not_found"</c>); <see cref="Message"/> is a human-readable
/// description suitable for logs or user-facing surfaces.
/// </summary>
/// <param name="Code">Stable machine-readable error code.</param>
/// <param name="Message">Human-readable description.</param>
/// <param name="Cause">Underlying exception, if any.</param>
public sealed record Error(string Code, string Message, Exception? Cause = null);