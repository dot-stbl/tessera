using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Tessera.Host.OpenApi;

/// <summary>
///     OpenAPI operation transformer that injects the canonical RFC 9457
///     ProblemDetails responses (400 / 404 / 409 / 500 / 502 / 503 / 504)
///     onto every controller action. Complements <c>[ProducesResponseType&lt;T&gt;]</c>
///     which documents the 2xx success shape per-action. Together the OpenAPI
///     document and the wire behaviour stay in lock-step without per-endpoint
///     error annotations.
/// </summary>
/// <remarks>
///     Uses <c>Responses.TryAdd</c> so explicit per-action
///     <c>[ProducesResponseType&lt;ProblemDetails&gt;]</c> attributes (rare, but
///     allowed for endpoints that override the default code/title with a
///     machine-readable discriminator) win over the global injection.
/// </remarks>
public sealed class ProblemDetailsResponsesTransformer : IOpenApiOperationTransformer
{
    private static readonly OpenApiMediaType ProblemJsonMediaType = new()
    {
        Schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Description = "RFC 9457 problem detail (https://www.rfc-editor.org/rfc/rfc9457).",
        },
    };

    /// <inheritdoc />
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        ProblemDetailsResponseShims.AddResponse(operation, "400", "Bad Request: request validation failed (model binding or FluentValidation).");
        ProblemDetailsResponseShims.AddResponse(operation, "404", "Not Found: the requested resource does not exist.");
        ProblemDetailsResponseShims.AddResponse(operation, "409", "Conflict: the request collides with the current state of the target resource.");
        ProblemDetailsResponseShims.AddResponse(operation, "500", "Internal Server Error: an unexpected server-side failure occurred.");
        ProblemDetailsResponseShims.AddResponse(operation, "502", "Bad Gateway: upstream provider unavailable.");
        ProblemDetailsResponseShims.AddResponse(operation, "503", "Service Unavailable: degraded state (e.g. one or more providers unreachable).");
        ProblemDetailsResponseShims.AddResponse(operation, "504", "Gateway Timeout: upstream provider exceeded its timeout budget.");
        return Task.CompletedTask;
    }
}
