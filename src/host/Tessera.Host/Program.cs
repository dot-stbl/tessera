using Microsoft.AspNetCore.Http;
using System.Text.Json.Serialization;
using Tessera.Host.Errors;
using Tessera.Modules.Discovery.DependencyInjection;
using Tessera.Modules.Health.DependencyInjection;
using Tessera.Modules.Logs.DependencyInjection;
using Tessera.Modules.Traces.DependencyInjection;
using Tessera.Providers.Victoria.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

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

app.MapGet("/",
    () => Results.Ok(new
    {
        name = "tessera",
        version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0",
        docs = "See .agents/docs/architecture.md",
    }));

app.MapControllers();

app.Run();