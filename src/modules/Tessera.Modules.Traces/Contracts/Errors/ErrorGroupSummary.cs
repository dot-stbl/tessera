namespace Tessera.Modules.Traces.Contracts.Errors;

/// <summary>
///     One grouped error row for the errors inbox: exception type +
///     normalized message fingerprint, occurrence count, sample trace ids.
/// </summary>
public sealed record ErrorGroupSummary
{
    /// <summary>Stable group key (<c>type|normalizedMessage</c>).</summary>
    public required string Key { get; init; }

    /// <summary><c>exception.type</c>, or null when status-error without event.</summary>
    public string? ExceptionType { get; init; }

    /// <summary>
    ///     Representative exception message (first sample, not necessarily normalized).
    /// </summary>
    public string? Message { get; init; }

    /// <summary>Number of span-level errors in this group across fetched details.</summary>
    public int Count { get; init; }

    /// <summary>Up to five sample trace ids that contributed to this group.</summary>
    public required IReadOnlyList<string> SampleTraceIds { get; init; }
}
