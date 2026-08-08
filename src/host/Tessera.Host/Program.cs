using Microsoft.Extensions.Options;
using Spectre.Console;
using Tessera.Banner;
using Tessera.Host.Storage;
using Tessera.Modules.Discovery.DependencyInjection;
using Tessera.Modules.Health.DependencyInjection;
using Tessera.Modules.Logs.DependencyInjection;
using Tessera.Modules.Traces.DependencyInjection;
using Tessera.Providers.Victoria.DependencyInjection;
using Tessera.Shared.Authentication;
using Tessera.Shared.Kernel.Api;
using Tessera.Shared.Kernel.Configuration.Layout;
using Tessera.Shared.Kernel.Configuration.Options;
using Tessera.Shared.Kernel.Configuration.Source;
using Tessera.Shared.Telemetry;
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
// Dev = appsettings-style: load tessera.toml (+ optional tessera.local.toml)
// from the host ContentRoot (project directory under `dotnet run`), not from
// process cwd. Production still falls back to OS layout / TESSERA_CONFIG when
// content-root files are absent. See TesseraConfigPaths.ResolveMainPath.
builder.Configuration.AddTesseraConfiguration(builder.Environment.ContentRootPath);

// --------------------------------------------------------------------
// File-system layout ([fs_layout] section in tessera.toml)
// --------------------------------------------------------------------
// Resolves platform-appropriate config / data / state / cache / log / temp
// directories via OperatingSystem.IsLinux / IsWindows. Override per
// ADR-0001 D7 ([fs_layout]/provider = "linux" | "windows" | env
// TESSERA_FS_LAYOUT_PROVIDER). Used by the Preferences module to place
// the SQLite file under DataDirectory (or %LOCALAPPDATA%\tessera on
// Windows).
builder.Services.AddTesseraFileSystemLayout(builder.Configuration);

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
// Storage options ([storage] section in tessera.toml)
// --------------------------------------------------------------------
// MVP-01 backs every persistent aggregate (UserPreference first) with
// SQLite via Microsoft.EntityFrameworkCore.Sqlite 10.0.10. Validation
// fails startup if [storage]/provider is something other than "sqlite"
// — the host composition root only knows how to wire SQLite today.
builder.Services.AddOptions<StorageOptions>()
    .Bind(builder.Configuration.GetSection(StorageOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Single composition helper — resolves the SQLite connection string
// (operator-supplied or synthesized from IFileSystemLayout.DataDirectory + StorageOptions.FileName)
// and adds the Preferences module + PreferencesDbContext.
//
// Module composition must happen AFTER both StorageOptions is bound
// (so the connection string is resolvable) AND IFileSystemLayout is
// registered (so the synthesized path resolves to the OS-correct
// data directory).
builder.Services.AddPreferencesStorage();

// --------------------------------------------------------------------
// Tessera's own telemetry (Phase — observability)
// --------------------------------------------------------------------
// One OTel pipeline that covers traces + metrics via the shared
// Tessera.Shared.Telemetry installer. Bound from [telemetry] in
// tessera.toml with sane defaults (otlp_endpoint=http://localhost:4317
// matches the compose stack collector). See diagnostics.md for
// ActivitySource naming convention (Tessera.<Module>) + metric
// naming (tessera.<noun>.<quantity>).
builder.Services
    .AddOptions<TelemetryOptions>()
    .Bind(builder.Configuration.GetSection(TelemetryOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOpenTelemetry()
    .ConfigureTesseraTelemetry(
        builder.Configuration
            .GetSection(TelemetryOptions.SectionName)
            .Get<TelemetryOptions>() ?? new TelemetryOptions());

// --------------------------------------------------------------------
// Controllers + JSON
// --------------------------------------------------------------------
// MVC's AddControllers registers the API conventions, model binding,
// validation filter, [ProducesResponseType] → OpenAPI mapping. AddApplicationPart
// (one per module) discovers controllers in each module assembly so
// Tessera.Host doesn't need a direct type reference — the composition-root
// boundary stays clean.
//
// The wire format itself is defined once in TesseraJsonOptions and applied to
// both serialization pipelines — MVC here, raw System.Text.Json in
// TesseraExceptionHandler — so enum casing and naming policy cannot drift
// between a normal response and an error body.
builder.Services
    .AddControllers()
    .AddJsonOptions(static options => TesseraJsonOptions.ApplyTo(options.JsonSerializerOptions))
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
// Persistence schema bootstrap (Phase 5c)
// --------------------------------------------------------------------
// Apply pending EF Core migrations (or EnsureCreated when no
// migration has been generated yet) before the host starts
// accepting requests. Failure here aborts startup — the host is
// not "started but broken", it's "not started" — so the operator
// sees the failure in startup logs rather than 502s on the first
// HTTP request.
await PreferencesStorageInstaller
    .EnsurePreferencesSchemaAsync(app.Services);

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

// --------------------------------------------------------------------
// Front-end bundle (Phase 6 / MVP-02)
// --------------------------------------------------------------------
// wwwroot/ is populated at publish time by the multi-stage Dockerfile
// (compose/Dockerfile fetches the bun-built FE and copies it into
// Tessera.Host/wwwroot/ before `dotnet publish`). Static files are
// served from the same origin as the API so the SPA can fetch
// /api/v1/* directly without CORS preflight noise. MapFallbackToFile
// sends unmatched routes to index.html so client-side routing
// works on direct deep-link loads.
// Single root policy per ASP.NET Core defaults — /index.html and
// /assets/* short-circuit before the fallback.
app.UseStaticFiles();
app.MapFallbackToFile("index.html");

app.Run();
