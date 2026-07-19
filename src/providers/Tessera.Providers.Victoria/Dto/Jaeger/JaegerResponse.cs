namespace Tessera.Providers.Victoria.Dto.Jaeger;

/// <summary>
///     Jaeger-style envelope used by VictoriaTraces endpoints. Wraps the
///     response payload (<see cref="Data" />) with pagination metadata
///     (<see cref="Total" /> / <see cref="Limit" /> / <see cref="Offset" />)
///     and optional <c>errors</c> array.
/// </summary>
/// <typeparam name="T">
///     Payload element type (string for service/operation lists,
///     <see cref="Trace.JaegerTrace" /> for trace search/get-by-id).
/// </typeparam>
public sealed record JaegerResponse<T>(
    IReadOnlyList<T> Data,
    int Total,
    int Limit,
    int Offset,
    object? Errors);
