using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Spectre.Console;
using Tessera.Banner;
using Tessera.Modules.Discovery.DependencyInjection;
using Tessera.Modules.Health.DependencyInjection;
using Tessera.Modules.Logs.DependencyInjection;
using Tessera.Modules.Traces.DependencyInjection;
using Tessera.Providers.Victoria.DependencyInjection;
using Tessera.Shared.Authentication;
using Tessera.Shared.Kernel.Configuration.Options;
using Tessera.Shared.Kernel.Configuration.Source;
using Tessera.Shared.Web;

// --------------------------------------------------------------------
// Banner CLI flags (--help, --version, --banner)
// --------------------------------------------------------------------
// Per the .stbl brand ("hidden file in Unix. ls won't show it. ls -la
// will"), the banner does NOT show on every startup. It only renders
// when the user passes --help, --version, or --banner. We parse these
// flags BEFORE WebApplication.CreateBuilder so they exit cleanly
// without the host's pipeline spinning up.
var bannerArgs = BannerCliArgs.Parse(args);
if (bannerArgs.ShowHelp || bannerArgs.ShowVersion || bannerArgs.ShowBanner)
{
    BannerRenderer.WriteTo(AnsiConsole.Console, BannerOptions.ForHost(bannerArgs.NoColor) with { Variant = bannerArgs.Variant });
    if (bannerArgs.ShowHelp)
    {
        AnsiConsole.Console.MarkupLine("[grey]Usage:[/] tessera [options]");
        AnsiConsole.Console.MarkupLine("[grey]Options:[/]");
        AnsiConsole.Console.MarkupLine("  [grey]--help, -h[/]              show this help");
        AnsiConsole.Console.MarkupLine("  [grey]--version, -v[/]           show version");
        AnsiConsole.Console.MarkupLine("  [grey]--banner[/]                 print the banner and exit");
        AnsiConsole.Console.MarkupLine("  [grey]--variant <name>[/]         block | minimal (default: block)");
        AnsiConsole.Console.MarkupLine("  [grey]--no-color[/]               disable ANSI colour output");
    }
    return;
}

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
// Server options ([server] section in tessera.toml)
// --------------------------------------------------------------------
// Bind [server]/host + [server]/port from TOML into ServerOptions.
// ValidateOnStart fails startup if port is outside the 1990-2120 reserved
// range (see .agents/rules/coding/project-ports.md) or annotation breaks,
// rather than failing on the first HTTP request.
builder.Services.AddOptions<ServerOptions>()
    .Bind(builder.Configuration.GetSection(ServerOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

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
    .AddJsonOptions(static options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
    .AddApplicationPart(typeof(Tessera.Modules.Health.Controllers.HealthController).Assembly)
    .AddApplicationPart(typeof(Tessera.Modules.Discovery.Controllers.DiscoveryController).Assembly)
    .AddApplicationPart(typeof(Tessera.Modules.Traces.Controllers.TracesController).Assembly)
    .AddApplicationPart(typeof(Tessera.Modules.Logs.Controllers.LogsController).Assembly);

// --------------------------------------------------------------------
// Web infrastructure (ProblemDetails + OpenAPI + Scalar)
// --------------------------------------------------------------------
// Tessera.Shared.Web encapsulates the global RFC 9457 handler, the
// problem-details response transformer, and the Scalar mount. Host no
// longer references those types directly — composition stays declarative.
builder.Services.AddTesseraWebInfrastructure();

// --------------------------------------------------------------------
// Authentication (multi-provider framework, guest + admin-bearer in MVP-01)
// --------------------------------------------------------------------
// Per ADR-0001 Decision 1, multiple auth schemes coexist; the default
// scheme is taken from [auth]/default_scheme in tessera.toml (defaults
// to "guest" so anonymous reads work without explicit [Authorize]).
// AdminBearer is registered when [auth.providers.admin-bearer]/enabled
// is true; LDAP (4b) and Keycloak (4c) will plug into the same framework.
builder.Services.AddTesseraAuthentication(builder.Configuration);
builder.Services.AddAuthorization();

// --------------------------------------------------------------------
// Module DI
// --------------------------------------------------------------------
// Each AddXxxModule registers the module's Mapperly mapper + any per-module
// services. AddVictoriaProvider wires the concrete IHealthProvider /
// ITraceProvider / ILogProvider / IDiscoveryProvider implementations from
// Tessera.Providers.Victoria — modules stay provider-agnostic (project-deps-and-tests.md
// provider isolation rule).
builder.Services
    .AddHealthModule()
    .AddDiscoveryModule()
    .AddTracesModule()
    .AddLogsModule()
    .AddVictoriaProvider(builder.Configuration);

var app = builder.Build();

// --------------------------------------------------------------------
// Kestrel bind
// --------------------------------------------------------------------
// Read the resolved ServerOptions (already validated) and apply to Kestrel.
// Replaces ASP.NET's default URL discovery (ASPNETCORE_URLS env + launchSettings.json)
// so config is the single source for Host + Port. app.Urls is the canonical
// way in .NET 10 to set the listen URL after Build.
var serverOptions = app.Services.GetRequiredService<IOptions<ServerOptions>>().Value;
app.Urls.Clear();
app.Urls.Add($"http://{serverOptions.Host}:{serverOptions.Port}");

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
    static () => Results.Ok(new
    {
        name = "tessera",
        version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0",
        docs = "See .agents/docs/architecture.md",
    }));

app.UseTesseraOpenApi();
app.MapControllers();

app.Run();
