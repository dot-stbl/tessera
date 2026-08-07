using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tessera.Providers.Victoria.Dto.Prometheus.Query;

/// <summary>
///     Prometheus <c>data</c> object. <see cref="Result" /> is a JSON array whose
///     element shape depends on <see cref="ResultType" /> (matrix / vector / scalar).
/// </summary>
public sealed record PromQueryData(
    [property: JsonPropertyName("resultType")] string ResultType,
    [property: JsonPropertyName("result")] JsonElement Result);
