using Spectre.Console;

namespace Tessera.Banner.BannerVariants;

/// <summary>
///     Plain-text banner variant for narrow terminals (&lt; 80 cols)
///     where FigletText overflows. No FigletText, no colour — the
///     .stbl brand minimalism at its purest.
/// </summary>
internal static class MinimalVariant
{
    /// <summary>
    ///     Render the Minimal variant to <paramref name="console" />.
    ///     Three centered lines: name, version, tagline.
    ///     <paramref name="noColor" /> is accepted for API parity with
    ///     <see cref="BlockVariant.Render" /> but the minimal variant is
    ///     intentionally colorless.
    /// </summary>
    public static void Render(IAnsiConsole console, string tagline, bool noColor)
    {
        console.WriteLine("tessera");
        console.WriteLine("v0.0.0");
        console.WriteLine();
        console.WriteLine(tagline);
    }
}
