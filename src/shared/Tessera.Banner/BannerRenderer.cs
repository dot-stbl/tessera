using Spectre.Console;

namespace Tessera.Banner;

/// <summary>
///     Top-level entry point for emitting a Tessera startup banner.
///     Pure function over <see cref="IAnsiConsole" /> so unit tests
///     can drive the renderer through <c>Spectre.Console.Testing.TestConsole</c>
///     and assert the captured output.
/// </summary>
/// <remarks>
///     <para>
///         The banner is opt-in — <see cref="Tessera.Banner.BannerCliArgs" />
///         decides whether to call into this type at all. Per the
///         .stbl brand guide ("hidden file in Unix. ls won't show
///         it. ls -la will") the banner does NOT show on every
///         startup; it only shows when the user passes
///         <c>--help</c>, <c>--version</c>, or <c>--banner</c>.
///     </para>
///     <para>
///         Each variant is implemented in its own file in
///         <c>BannerVariants/</c>; this class is the dispatcher only
///         and contains no rendering logic of its own (per
///         code-shape §1a, no private methods in production classes).
///     </para>
/// </remarks>
public static class BannerRenderer
{
    /// <summary>
    ///     Default Tessera tagline used when <see cref="BannerOptions.Tagline" />
    ///     is <c>null</c>. Matches the README "What it does" line.
    /// </summary>
    public const string DefaultTagline = "self-hosted APM UI for the Victoria stack";

    /// <summary>
    ///     Tessera-specific red accent — the only colour the banner
    ///     emits by default. Differs from the .stbl brand (pure B&amp;W)
    ///     so consumers reading the source can tell the two apart.
    /// </summary>
    public const string AccentRed = "#C8102E";

    /// <summary>
    ///     Render the banner to <paramref name="console" /> with the
    ///     supplied <paramref name="options" />. No state, no side
    ///     effects — pure output.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public static void WriteTo(IAnsiConsole console, BannerOptions options)
    {
        ArgumentNullException.ThrowIfNull(console);
        ArgumentNullException.ThrowIfNull(options);

        var tagline = options.Tagline ?? DefaultTagline;
        var version = ResolveVersion();
        var ruleColor = options.NoColor ? Color.Default : Color.Grey;

        // Header line with the "tessera v0.1.0 · by stbl" mini-stamp.
        var header = options.NoColor
            ? $"tessera {version}  ·  by stbl"
            : $"[bold]tessera[/] [grey]{version}[/]  ·  by stbl";
        var rule = new Rule(header)
        {
            Justification = Justify.Center,
        };
        if (!options.NoColor)
        {
            rule.Style = ruleColor;
        }
        console.Write(rule);
        console.WriteLine();

        switch (options.Variant)
        {
            case BannerVariantKind.Block:
                BannerVariants.BlockVariant.Render(console, tagline, options.NoColor);
                break;
            case BannerVariantKind.Minimal:
                BannerVariants.MinimalVariant.Render(console, tagline, options.NoColor);
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(options),
                    options.Variant,
                    "Unknown banner variant kind.");
        }

        console.WriteLine();
    }

    /// <summary>
    ///     Resolve the assembly informational-version (e.g. "0.1.0")
    ///     from the calling assembly. Falls back to <c>"0.0.0"</c> on
    ///     anything that throws — the banner must never crash the host.
    /// </summary>
    private static string ResolveVersion()
    {
        try
        {
            var version = typeof(BannerRenderer).Assembly
                .GetName()
                .Version;
            if (version is null || (version.Major == 0 && version.Minor == 0 && version.Build < 0))
            {
                return "0.0.0";
            }

            return $"{version.Major}.{version.Minor}.{version.Build}";
        }
        catch
        {
            return "0.0.0";
        }
    }
}
