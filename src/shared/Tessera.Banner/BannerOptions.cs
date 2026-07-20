namespace Tessera.Banner;

/// <summary>
///     Tunable options for <see cref="BannerRenderer.WriteTo" />. Records
///     are passed by-value into the renderer so the same
///     <see cref="BannerOptions" /> instance can be shared across
///     the host and the load generator without state leak.
/// </summary>
/// <param name="Variant">
///     Which <see cref="BannerVariantKind" /> to render. Defaults to
///     <see cref="BannerVariantKind.Block" />. Pick
///     <see cref="BannerVariantKind.Minimal" /> for narrow terminals
///     (&lt; 80 columns) where the FigletText in Block overflows.
/// </param>
/// <param name="NoColor">
///     When true, the renderer emits plain text — no ANSI colour
///     sequences. Auto-true if the <c>NO_COLOR</c> environment
///     variable is set (per the no-color.org convention).
/// </param>
/// <param name="Tagline">
///     One-line subtitle rendered below the title. The .stbl brand
///     canonical tagline is "self-hosted APM UI for the Victoria stack";
///     consumers can override for embedding context.
/// </param>
public sealed record BannerOptions(
    BannerVariantKind Variant = BannerVariantKind.Block,
    bool NoColor = false,
    string? Tagline = null)
{
    /// <summary>
    ///     Default <see cref="BannerOptions" /> for the Tessera host
    ///     and the LoadGen CLI: Block variant, NoColor follows
    ///     <c>NO_COLOR</c>, tagline auto-derived.
    /// </summary>
    public static BannerOptions Default { get; } = new();

    /// <summary>
    ///     Build a <see cref="BannerOptions" /> honouring the
    ///     <c>NO_COLOR</c> environment convention. Pass
    ///     <paramref name="explicitNoColor" /> = true to force-disable
    ///     colour (e.g. from a <c>--no-color</c> CLI flag); otherwise
    ///     the value is read from <c>NO_COLOR</c> on the process
    ///     environment.
    /// </summary>
    public static BannerOptions ForHost(bool explicitNoColor = false)
    {
        return new BannerOptions(
            Variant: BannerVariantKind.Block,
            NoColor: explicitNoColor || IsNoColorSet(),
            Tagline: null);
    }

    private static bool IsNoColorSet()
    {
        var value = Environment.GetEnvironmentVariable("NO_COLOR");
        return !string.IsNullOrWhiteSpace(value) && value != "0";
    }
}

/// <summary>
///     Catalog of banner variants. Adding a new variant is a 1-type
///     concern: drop a new <c>XxxVariant.cs</c> in
///     <c>BannerVariants/</c>, add an enum member, switch on it in
///     <see cref="Tessera.Banner.BannerVariants.BlockVariant" /> (or
///     a new dispatcher). Keep the enum small — the .stbl brand is
///     minimal, two variants is plenty.
/// </summary>
public enum BannerVariantKind
{
    /// <summary>FigletText 'tessera' + bordered text + red accent divider.</summary>
    Block = 0,

    /// <summary>Plain text frame, no Figlet — fallback for narrow terminals (&lt; 80 cols).</summary>
    Minimal = 1,
}
