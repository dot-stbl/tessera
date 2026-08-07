using System.Text.Json.Serialization;

namespace Tessera.Providers.Victoria.Dto.Prometheus.Labels;

/// <summary>
///     Prometheus HTTP API envelope for <c>/label/{name}/values</c>.
/// </summary>
public sealed record PromLabelValuesResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("data")] IReadOnlyList<string>? Data,
    [property: JsonPropertyName("errorType")] string? ErrorType,
    [property: JsonPropertyName("error")] string? Error);
