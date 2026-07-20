using Spectre.Console;

namespace Tessera.Banner.BannerVariants;

/// <summary>
///     Default Tessera banner variant: FigletText "tessera" on top,
///     a bordered text frame with the tagline and a red-accent divider,
///     and a bottom rule. Designed for terminals 80+ columns wide.
///     </summary>
internal static class BlockVariant
{
    /// <summary>
    ///     Render the Block variant to <paramref name="console" />.
    ///     Honours <paramref name="noColor" /> by skipping all
    ///     <c>[...]</c> Spectre.Console markup tags.
    /// </summary>
    public static void Render(IAnsiConsole console, string tagline, bool noColor)
    {
        // Title in FigletText — Spectre.Console's bundled small font
        // fits in 80 columns at default size.
        var titleStyle = noColor
            ? "tessera"
            : "[bold]tessera[/]";

        console.Write(new FigletText(titleStyle).LeftJustified());
        console.WriteLine();

        // Tagline with red accent on the version suffix.
        var taglineText = noColor
            ? tagline
            : $"{tagline}  [bold {BannerRenderer.AccentRed}]· v0.0.0[/]";
        console.MarkupLine(taglineText);
        console.WriteLine();
    }
}
