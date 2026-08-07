namespace Tessera.Shared.Kernel.Analysis.Errors;

/// <summary>
///     Exception attributes extracted from a span event named
///     <c>exception</c> (OpenTelemetry semantic conventions).
/// </summary>
/// <param name="Type"><c>exception.type</c>, or null when absent.</param>
/// <param name="Message"><c>exception.message</c>, or null when absent.</param>
/// <param name="Stacktrace"><c>exception.stacktrace</c>, or null when absent.</param>
public sealed record ExceptionPayload(
    string? Type,
    string? Message,
    string? Stacktrace);
