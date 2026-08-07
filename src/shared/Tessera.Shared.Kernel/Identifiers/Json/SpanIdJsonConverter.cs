using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tessera.Shared.Kernel.Identifiers.Json;

/// <summary>
///     Serializes <see cref="SpanId" /> as a bare JSON string. See
///     <see cref="TraceIdJsonConverter" /> for why.
/// </summary>
public sealed class SpanIdJsonConverter : JsonConverter<SpanId>
{
    /// <inheritdoc />
    public override SpanId? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        var value = reader.GetString();
        return value is null ? null : new SpanId(value);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, SpanId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}
