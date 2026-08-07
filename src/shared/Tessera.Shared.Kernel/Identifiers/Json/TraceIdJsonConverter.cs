using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tessera.Shared.Kernel.Identifiers.Json;

/// <summary>
///     Serializes <see cref="TraceId" /> as a bare JSON string rather than the
///     <c>{ "value": "…" }</c> object a single-property record would produce by
///     default.
///     <para>
///         The wrapper exists to stop trace and span ids being mixed up inside
///         C#; it is not part of the API. Letting it reach the wire pushes the
///         unwrapping onto every consumer — a generated TypeScript client would
///         type <c>traceId</c> as an object and every call site would read
///         <c>trace.traceId.value</c> while the id is also a plain string in the
///         URL it came from.
///     </para>
/// </summary>
public sealed class TraceIdJsonConverter : JsonConverter<TraceId>
{
    /// <inheritdoc />
    public override TraceId? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        var value = reader.GetString();
        return value is null ? null : new TraceId(value);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, TraceId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}
