var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
    name = "tessera",
    version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0",
    docs = "See .agents/docs/architecture.md",
}));

app.Run();
