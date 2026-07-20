using Spectre.Console;
using Tessera.Banner;
using Tessera.LoadGen;

// Banner CLI flags run BEFORE GeneratorOptionsParser.Parse(args) so
// the user gets the banner + help on typos without us parsing both
// schemas in one go. Per the .stbl brand ("hidden file in Unix. ls
// won't show it. ls -la will"), the banner does NOT show on every
// startup; it only renders when --help, --version, or --banner is
// passed.
var bannerArgs = BannerCliArgs.Parse(args);
if (bannerArgs.ShowHelp || bannerArgs.ShowVersion || bannerArgs.ShowBanner)
{
    BannerRenderer.WriteTo(AnsiConsole.Console, BannerOptions.ForHost(bannerArgs.NoColor) with { Variant = bannerArgs.Variant });
    if (bannerArgs.ShowHelp)
    {
        AnsiConsole.Console.MarkupLine("[grey]Usage:[/] tessera-loadgen [options]");
        AnsiConsole.Console.MarkupLine("[grey]Options:[/]");
        AnsiConsole.Console.MarkupLine("  [grey]--help, -h[/]              show this help");
        AnsiConsole.Console.MarkupLine("  [grey]--version, -v[/]           show version");
        AnsiConsole.Console.MarkupLine("  [grey]--banner[/]                 print the banner and exit");
        AnsiConsole.Console.MarkupLine("  [grey]--variant <name>[/]         block | minimal (default: block)");
        AnsiConsole.Console.MarkupLine("  [grey]--no-color[/]               disable ANSI colour output");
        AnsiConsole.Console.MarkupLine("");
        AnsiConsole.Console.MarkupLine("[grey]Generator options:[/]");
        AnsiConsole.Console.MarkupLine("  [grey]--endpoint URL[/]           OTLP endpoint (default: http://localhost:4317)");
        AnsiConsole.Console.MarkupLine("  [grey]--services a,b,c[/]         comma-separated service names");
        AnsiConsole.Console.MarkupLine("  [grey]--rate N[/]                 operations per second");
        AnsiConsole.Console.MarkupLine("  [grey]--duration 30s[/]           total run time (0 = until Ctrl+C)");
        AnsiConsole.Console.MarkupLine("  [grey]--scenario <name>[/]       simple | fanout | saga");
    }
    return 0;
}

return await Generator.RunAsync(GeneratorOptionsParser.Parse(args), CancellationToken.None);
