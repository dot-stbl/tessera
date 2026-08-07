using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Scalar.AspNetCore;
using Tessera.Shared.Web.Errors;
using Tessera.Shared.Web.OpenApi;

namespace Tessera.Shared.Web;

/// <summary>
///     Composition-root extension for the cross-cutting web infrastructure:
///     RFC 9457 ProblemDetails pipeline (IExceptionHandler + IProblemDetailsService)
///     and the OpenAPI document provider with auto-injected problem-details
///     responses + Scalar UI mount. Centralises the wiring that used to live
///     inline in <c>Program.cs</c>; the host now reads as a flat chain of
///     feature-level <c>AddXxxModule()</c> + one
///     <c>AddTesseraWebInfrastructure()</c>.
/// </summary>
public static class WebInstallerExtensions
{
    /// <summary>
    ///     Register ProblemDetails + OpenAPI + Scalar infrastructure.
    ///     <list type="bullet">
    ///         <item><c>AddProblemDetails</c> — RFC 9457 body for status-code-page branches (404/415/etc.).</item>
    ///         <item><c>AddExceptionHandler&lt;TesseraExceptionHandler&gt;</c> — translates ProviderException / NotFound / Timeout into ProblemDetails bodies.</item>
    ///         <item><c>AddOpenApi</c> + <see cref="ProblemDetailsResponsesTransformer" /> — auto-injects 400/404/409/500/502/503/504 onto every operation so per-endpoint [ProducesResponseType&lt;ProblemDetails&gt;] is redundant.</item>
    ///         <item><see cref="WireShapeSchemaTransformer" /> — describes identifier wrappers and int64 as they actually serialize, so generated clients match the wire.</item>
    ///     </list>
    /// </summary>
    public static IServiceCollection AddTesseraWebInfrastructure(this IServiceCollection services)
    {
        services.AddProblemDetails();
        services.AddExceptionHandler<TesseraExceptionHandler>();
        services.AddOpenApi(static options =>
        {
            options.AddOperationTransformer<ProblemDetailsResponsesTransformer>()
                .AddSchemaTransformer<WireShapeSchemaTransformer>();
        });
        return services;
    }

    /// <summary>
    ///     Mount the OpenAPI document (at <c>/openapi/v1.json</c>) and the
    ///     Scalar UI (at <c>/scalar/v1</c>). Scalar 2.16 auto-discovers the
    ///     document registered via <see cref="AddTesseraWebInfrastructure" />.
    /// </summary>
    public static WebApplication UseTesseraOpenApi(this WebApplication app)
    {
        app.MapOpenApi();
        app.MapScalarApiReference();
        return app;
    }
}
