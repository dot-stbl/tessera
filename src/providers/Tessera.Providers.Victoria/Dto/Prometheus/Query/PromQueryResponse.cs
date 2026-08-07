using System.Text.Json.Serialization;

namespace Tessera.Providers.Victoria.Dto.Prometheus.Query;

/// <summary>
///     Prometheus HTTP API envelope for <c>/query</c> and <c>/query_range</c>.
/// </summary>
public sealed record PromQueryResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("data")] PromQueryData? Data,
    [property: JsonPropertyName("errorType")] string? ErrorType,
    [property: JsonPropertyName("error")] string? Error);
