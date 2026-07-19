using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Scalar.AspNetCore;
using System.Text.Json.Serialization;
using Tessera.Host.Auth;
using Tessera.Host.Errors;
using Tessera.Host.OpenApi;
using Tessera.Modules.Discovery.DependencyInjection;
using Tessera.Modules.Health.DependencyInjection;
using Tessera.Modules.Logs.DependencyInjection;
using Tessera.Modules.Traces.DependencyInjection;
using Tessera.Providers.Victoria.DependencyInjection;
using Tessera.Shared.Kernel.Configuration;

var builder = WebApplication.CreateBuilder(args);

// --------------------------------------------------------------------
// Configuration
// --------------------------------------------------------------------
// AddTesseraConfiguration must run before any other configuration source so
// defaults in code remain the lowest-precedence layer. TesseraConfigPaths
// resolves the main `tessera.toml` per the 4-level lookup (TESSERA_CONFIG
// env > /etc/tessera > XDG > cwd), then chains the optional
// `tessera.local.toml` override in the same directory.
builder.Configuration.AddTesseraConfiguration();

// --------------------------------------------------------------------
// Controllers + JSON
// --------------------------------------------------------------------
// MVC's AddControllers registers the API conventions, model binding,
// validation filter, [ProducesResponseType] → OpenAPI mapping. AddApplicationPart
// (one per module) discovers controllers in each module assembly so
// Tessera.Host doesn't need a direct type reference — the composition-root
// boundary stays clean.
//
// JsonStringEnumConverter serializes enums (HealthStatus, TraceStatus) as
// strings in the wire format instead of ints. The Plexor / OpenAPI convention;
// allows FE to deserialize case-insensitively while keeping C# enums
// strongly-typed.
builder.Services
    .AddControllers()
    .AddJsonOptions(static options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    })
    .AddApplicationPart(typeof(Tessera.Modules.Health.Controllers.HealthController).Assembly)
    .AddApplicationPart(typeof(Tessera.Modules.Discovery.Controllers.DiscoveryController).Assembly)
    .AddApplicationPart(typeof(Tessera.Modules.Traces.Controllers.TracesController).Assembly)
    .AddApplicationPart(typeof(Tessera.Modules.Logs.Controllers.LogsController).Assembly);

// --------------------------------------------------------------------
// ProblemDetails pipeline (RFC 9457)
// --------------------------------------------------------------------
// AddProblemDetails wires IProblemDetailsService for any status-code-page
// branch (404 from constraint miss, 415 from content-type miss).
// AddExceptionHandler<TesseraExceptionHandler>() registers the global handler
// that catches ProviderException / ProviderNotFoundException /
// ProviderTimeoutException and writes a ProblemDetails body.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<TesseraExceptionHandler>();

// --------------------------------------------------------------------
// OpenAPI doc + ProblemDetails transformer + Scalar UI
// --------------------------------------------------------------------
// AddOpenApi wires Microsoft.AspNetCore.OpenApi source-generated document
// provider. The ProblemDetailsResponsesTransformer injects the canonical
// RFC 9457 response shapes (400/404/409/500/502/503/504) onto every
// operation so the wire format and the OpenAPI doc stay in lock-step
// without per-endpoint [ProducesResponseType<ProblemDetails>] attributes.
// Scalar.AspNetCore 2.16 auto-discovers the OpenAPI doc registered above;
// MapScalarApiReference() mounts the API reference UI at /scalar/v1.
builder.Services.AddOpenApi(options =>
    options.AddOperationTransformer<ProblemDetailsResponsesTransformer>());

// --------------------------------------------------------------------
// Authentication (admin bearer — optional in MVP-01)
// --------------------------------------------------------------------
// Admin scheme is registered unconditionally so admin endpoints (Phase 5+)
// can decorate themselves with [Authorize(Policy = "admin")] and not need
// host changes. The handler reads the token from TESSERA_ADMIN_TOKEN env var;
// if the env var is unset the handler returns NoResult() for every request,
// so admin endpoints reject with 401 and the host still starts cleanly.
// This matches the MVP-01 "admin bearer optional" decision.
builder.Services
    .AddAuthentication("admin")
    .AddScheme<AdminBearerOptions, AdminBearerHandler>("admin", options =>
    {
        options.AdminToken = builder.Configuration["TESSERA_ADMIN_TOKEN"]
            ?? Environment.GetEnvironmentVariable("TESSERA_ADMIN_TOKEN");
    });
builder.Services.AddAuthorization();

// --------------------------------------------------------------------
// Module DI
// --------------------------------------------------------------------
// Each AddXxxModule registers the module's Mapperly mapper + any per-module
// services. AddVictoriaProvider (below) wires the concrete IHealthProvider /
// ITraceProvider / ILogProvider / IDiscoveryProvider implementations from
// Tessera.Providers.Victoria — modules stay provider-agnostic (PROJECT-DEP-AND-TESTS.MD
// provider isolation rule).
builder.Services
    .AddHealthModule()
    .AddDiscoveryModule()
    .AddTracesModule()
    .AddLogsModule()
    .AddVictoriaProvider(builder.Configuration);

var app = builder.Build();

// --------------------------------------------------------------------
// Error pipeline
// --------------------------------------------------------------------
// UseExceptionHandler reads from the registered IExceptionHandler chain
// (TesseraExceptionHandler for ProviderException, then falls through to the
// framework's default problem-details writer for unhandled exceptions).
// UseStatusCodePages covers status codes that the framework returns with
// no body (404 from route-constraint miss, 405 method-not-allowed, etc.).
app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/",
    () => Results.Ok(new
    {
        name = "tessera",
        version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0",
        docs = "See .agents/docs/architecture.md",
    }));

app.MapOpenApi();
app.MapScalarApiReference();
app.MapControllers();

app.Run();