using Microsoft.OpenApi;

namespace Tessera.Shared.Web.OpenApi;

/// <summary>
///     Pure helper that appends an <see cref="OpenApiResponse" /> entry to an
///     <see cref="OpenApiOperation" /> with the canonical
///     <c>application/problem+json</c> media type attached. Lives in a
///     <c>file static class</c> so the
///     <see cref="ProblemDetailsResponsesTransformer" /> stays focused on
///     running through the status-code catalog without carrying private
///     methods (code-shape.md §9 ban).
/// </summary>
internal static class ProblemDetailsResponseShims
{
    private static readonly OpenApiMediaType ProblemJsonMediaType = new()
    {
        Schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Description = "RFC 9457 problem detail (https://www.rfc-editor.org/rfc/rfc9457).",
        },
    };

    public static void AddResponse(OpenApiOperation operation, string statusCode, string description)
    {
        operation.Responses ??= new OpenApiResponses();
        operation.Responses.TryAdd(statusCode, new OpenApiResponse
        {
            Description = description,
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/problem+json"] = ProblemJsonMediaType,
            },
        });
    }
}
