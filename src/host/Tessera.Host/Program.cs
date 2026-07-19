using Tessera.Modules.Discovery.DependencyInjection;
using Tessera.Modules.Health.DependencyInjection;
using Tessera.Modules.Logs.DependencyInjection;
using Tessera.Modules.Traces.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddApplicationPart(typeof(Tessera.Modules.Health.Controllers.HealthController).Assembly)
    .AddApplicationPart(typeof(Tessera.Modules.Discovery.Controllers.DiscoveryController).Assembly)
    .AddApplicationPart(typeof(Tessera.Modules.Traces.Controllers.TracesController).Assembly)
    .AddApplicationPart(typeof(Tessera.Modules.Logs.Controllers.LogsController).Assembly);

builder.Services
    .AddHealthModule()
    .AddDiscoveryModule()
    .AddTracesModule()
    .AddLogsModule();

var app = builder.Build();

app.MapGet("/",
    () => Results.Ok(new
    {
        name = "tessera",
        version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0",
        docs = "See .agents/docs/architecture.md"
    }));

app.MapControllers();

app.Run();