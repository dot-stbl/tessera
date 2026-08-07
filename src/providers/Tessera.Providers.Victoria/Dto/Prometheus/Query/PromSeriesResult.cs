using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tessera.Providers.Victoria.Dto.Prometheus.Query;

/// <summary>
///     One matrix/vector series row: metric labels + values (or a single value).
///     Matrix uses <see cref="Values" />; vector uses <see cref="Value" />.
/// </summary>
public sealed record PromSeriesResult(
    [property: JsonPropertyName("metric")] IReadOnlyDictionary<string, string>? Metric,
    [property: JsonPropertyName("values")] IReadOnlyList<JsonElement>? Values,
    [property: JsonPropertyName("value")] JsonElement? Value);
