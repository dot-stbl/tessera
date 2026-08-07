namespace Tessera.Modules.Traces.Contracts;

/// <summary>
///     Wire DTO for an exception attached to a span on the request-view
///     response. Stacktrace is omitted on this list shape (can be huge).
/// </summary>
public sealed record TraceExceptionSummary
{
    /// <summary><c>exception.type</c>, or null when only status-error.</summary>
    public string? ExceptionType { get; init; }

    /// <summary><c>exception.message</c>, or null when absent.</summary>
    public string? ExceptionMessage { get; init; }

    /// <summary>Span id that carried the error (hex string).</summary>
    public required string SpanId { get; init; }

    /// <summary>Service name of the errored span.</summary>
    public required string Service { get; init; }

    /// <summary>Operation / span name of the errored span.</summary>
    public required string Operation { get; init; }
}
