using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Tessera.Shared.Kernel.Identifiers;

namespace Tessera.Shared.Web.OpenApi;

/// <summary>
///     Corrects two places where the schema generator describes the CLR type
///     instead of the shape that actually goes out on the wire. Both matter
///     because the document is the input to the front-end's type generation: a
///     wrong schema here becomes wrong TypeScript everywhere.
/// </summary>
public sealed class WireShapeSchemaTransformer : IOpenApiSchemaTransformer
{
    /// <inheritdoc />
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        // 1. Identifier wrappers. TraceIdJsonConverter / SpanIdJsonConverter
        //    write a bare string, but the generator only sees a record with one
        //    property and documents { "value": "…" }.
        var type = context.JsonTypeInfo.Type;
        if (type == typeof(TraceId) || type == typeof(SpanId))
        {
            schema.Type = JsonSchemaType.String;
            schema.Properties?.Clear();
            schema.Required?.Clear();
            return Task.CompletedTask;
        }

        // 2. Enums. JsonStringEnumConverter puts camelCase names on the wire,
        //    but the schema generator resolves its own serializer options and
        //    documents the underlying integer. A generated client would type
        //    `status` as number and compare it against strings that arrive at
        //    runtime — the same silent mismatch the converter's naming policy
        //    exists to prevent, reintroduced one layer up.
        if (type.IsEnum)
        {
            schema.Type = JsonSchemaType.String;
            schema.Format = null;
            schema.Enum = Enum.GetNames(type)
                .Select(static name => (JsonNode)JsonValue.Create(JsonNamingPolicy.CamelCase.ConvertName(name)))
                .ToList();
            return Task.CompletedTask;
        }

        // 3. Integers. The generator emits type: ["integer", "string"] for
        //    int32/int64, a hedge against JS losing precision above 2^53. Every
        //    integer we expose is a unix-millisecond timestamp, a duration, a
        //    limit or a count — all far inside the safe range — and the union
        //    forces `number | string` on the TypeScript side, so arithmetic on a
        //    duration needs a cast at every call site. Collapse to plain integer.
        if (schema.Type is { } declared
            && declared.HasFlag(JsonSchemaType.Integer)
            && declared.HasFlag(JsonSchemaType.String))
        {
            schema.Type = JsonSchemaType.Integer;
            schema.Pattern = null;
        }

        return Task.CompletedTask;
    }
}
