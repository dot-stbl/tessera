using Tessera.Shared.Kernel.Identifiers;

namespace Tessera.Shared.Kernel.Analysis.Errors;

/// <summary>
///     Span-level error derived from status and optional exception event
///     attributes (<c>exception.*</c> semantic conventions).
/// </summary>
/// <param name="SpanId">Span that carried the error status.</param>
/// <param name="Service">Service name from the span.</param>
/// <param name="Operation">Operation / span name.</param>
/// <param name="ExceptionType">
///     <c>exception.type</c> when an exception event is present; otherwise null.
/// </param>
/// <param name="ExceptionMessage">
///     <c>exception.message</c> when present; otherwise null.
/// </param>
/// <param name="ExceptionStacktrace">
///     <c>exception.stacktrace</c> when present; otherwise null.
/// </param>
public sealed record SpanError(
    SpanId SpanId,
    string Service,
    string Operation,
    string? ExceptionType,
    string? ExceptionMessage,
    string? ExceptionStacktrace);
