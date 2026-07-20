using Spectre.Console.Testing;
using Tessera.Banner;
using Xunit;

namespace Tessera.Shared.Kernel.Tests.Banner;

/// <summary>
///     Unit tests for <see cref="BannerRenderer" />. Uses
///     Spectre.Console's <see cref="TestConsole" /> to capture the
///     rendered output and assert the banner's contents without
///     depending on a real TTY.
/// </summary>
public sealed class BannerRendererTests
{
    /// <summary>
    ///     The Block variant of the banner always renders the
    ///     'tessera' FigletText — that is the only piece of surface
    ///     area the brand cares about, so the test is the entire
    ///     smoke check for the renderer.
    /// </summary>
    [Fact]
    public void WriteTo_WithBlockVariant_ContainsTesseraName()
    {
        var console = new TestConsole();

        BannerRenderer.WriteTo(console, new BannerOptions(BannerVariantKind.Block));

        var output = console.Output;
        Assert.Contains("tessera", output, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     The default tagline <see cref="BannerRenderer.DefaultTagline" />
    ///     is rendered below the title — sanity check that the
    ///     default-tagging path is wired (custom-tagline override is
    ///     tested by inference in the host wiring).
    /// </summary>
    [Fact]
    public void WriteTo_WithBlockVariant_ContainsTagline()
    {
        var console = new TestConsole();

        BannerRenderer.WriteTo(console, new BannerOptions(BannerVariantKind.Block));

        Assert.Contains(BannerRenderer.DefaultTagline, console.Output);
    }

    /// <summary>
    ///     The Minimal variant skips FigletText and emits only the
    ///     name + version + tagline as plain text. Test that the
    ///     name and tagline still appear so downstream consumers can
    ///     rely on those substrings regardless of variant.
    /// </summary>
    [Fact]
    public void WriteTo_WithMinimalVariant_ContainsNameAndTagline()
    {
        var console = new TestConsole();

        BannerRenderer.WriteTo(console, new BannerOptions(BannerVariantKind.Minimal));

        var output = console.Output;
        Assert.Contains("tessera", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(BannerRenderer.DefaultTagline, output);
    }

    /// <summary>
    ///     When <see cref="BannerOptions.NoColor" /> is true, the
    ///     renderer must NOT emit any ANSI escape sequences. CI logs
    ///     and redirected pipes rely on this; a regression that
    ///     accidentally leaks markup is the kind of bug that breaks
    ///     log parsing.
    /// </summary>
    [Fact]
    public void WriteTo_WithNoColorTrue_DoesNotEmitAnsiSequences()
    {
        var console = new TestConsole();

        BannerRenderer.WriteTo(console, new BannerOptions(NoColor: true));

        var output = console.Output;
        Assert.DoesNotContain("\u001b[", output);
    }
}
