using Tessera.Banner;
using Xunit;

namespace Tessera.Shared.Unit.Banner;

/// <summary>
///     Unit tests for <see cref="BannerCliArgs.Parse" />. Verifies the
///     hand-rolled CLI parser's flag recognition so a regression in
///     the parser breaks test cases loudly rather than silently
///     skipping the banner.
/// </summary>
public sealed class BannerCliArgsTests
{
    /// <summary>
    ///     <c>--help</c> must imply <c>ShowBanner</c> because the help
    ///     output is preceded by the banner — otherwise the user sees
    ///     a dry usage block with no brand.
    /// </summary>
    [Fact]
    public void Parse_WithHelpFlag_ReturnsShowBannerAndShowHelp()
    {
        var result = BannerCliArgs.Parse(["--help"]);

        Assert.True(result.ShowBanner);
        Assert.True(result.ShowHelp);
    }

    /// <summary>
    ///     <c>--version</c> implies <c>ShowBanner</c> for the same
    ///     reason as <c>--help</c> — the version line is only
    ///     meaningful in the context of the brand surface.
    /// </summary>
    [Fact]
    public void Parse_WithVersionFlag_ReturnsShowBannerAndShowVersion()
    {
        var result = BannerCliArgs.Parse(["--version"]);

        Assert.True(result.ShowBanner);
        Assert.True(result.ShowVersion);
    }

    /// <summary>
    ///     <c>--banner</c> alone sets <see cref="BannerCliResult.ShowBanner" />
    ///     only — the explicit-print use case (e.g. a script that
    ///     wants the banner without help text).
    /// </summary>
    [Fact]
    public void Parse_WithBannerFlag_ReturnsShowBanner()
    {
        var result = BannerCliArgs.Parse(["--banner"]);

        Assert.True(result.ShowBanner);
        Assert.False(result.ShowHelp);
        Assert.False(result.ShowVersion);
    }

    /// <summary>
    ///     <c>--variant minimal</c> switches the renderer to the
    ///     text-only fallback for narrow terminals.
    /// </summary>
    [Fact]
    public void Parse_WithVariantFlag_SetsVariant()
    {
        var result = BannerCliArgs.Parse(["--variant", "minimal"]);

        Assert.Equal(BannerVariantKind.Minimal, result.Variant);
    }

    /// <summary>
    ///     <c>--no-color</c> forces the renderer to plain text even
    ///     when stdout is a TTY.
    /// </summary>
    [Fact]
    public void Parse_WithNoColorFlag_SetsNoColorTrue()
    {
        var result = BannerCliArgs.Parse(["--no-color"]);

        Assert.True(result.NoColor);
    }

    /// <summary>
    ///     No flags means no banner — the .stbl brand minimalism
    ///     default behaviour (banner does NOT show on every
    ///     startup).
    /// </summary>
    [Fact]
    public void Parse_WithoutAnyFlag_ReturnsDefaults()
    {
        var result = BannerCliArgs.Parse([]);

        Assert.False(result.ShowBanner);
        Assert.False(result.ShowHelp);
        Assert.False(result.ShowVersion);
        Assert.Equal(BannerVariantKind.Block, result.Variant);
        Assert.False(result.NoColor);
    }

    /// <summary>
    ///     Unknown flags fall through silently — the consumer (Host /
    ///     LoadGen) raises its own error if the flag is mandatory for
    ///     its own config. The banner parser is permissive so it
    ///     doesn't shadow ASP.NET Core's CLI handling.
    /// </summary>
    [Fact]
    public void Parse_WithUnknownFlag_IgnoresItSilently()
    {
        var result = BannerCliArgs.Parse(["--definitely-not-a-banner-flag"]);

        Assert.False(result.ShowBanner);
    }
}
