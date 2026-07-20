namespace Tessera.Banner;

/// <summary>
///     Command-line argument parser for the banner. Surfaces a
///     <see cref="BannerCliResult" /> that the host / LoadGen
///     caller checks BEFORE starting any long-running work, so that
///     <c>--help</c> exits cleanly with the banner + usage text.
/// </summary>
/// <remarks>
///     <para>
///         The .stbl brand is minimal — there are only three user-
///         facing flags: <c>--banner</c> (print and exit),
///         <c>--help</c> (print help + banner, exit), <c>--version</c>
///         (print version + banner, exit). The variant flag and the
///         no-color flag are escape hatches for CI / non-tty contexts;
///         they don't add visual surface area, just control it.
///     </para>
///     <para>
///         Why CommandLineParser and not <c>System.CommandLine</c>:
///         smaller, fewer transitive dependencies, and already on the
///         developer machine via plexor's CLI tools. The parsing API
///         is stable (2.9.1 is from 2023, still maintained).
///     </para>
/// </remarks>
public static class BannerCliArgs
{
    /// <summary>
    ///     Parse <paramref name="args" /> into a <see cref="BannerCliResult" />.
    ///     Unrecognised flags are silently ignored — the consumer
    ///     (host / LoadGen) will then surface its own error / usage
    ///     if those flags are mandatory for its own config.
    /// </summary>
    public static BannerCliResult Parse(string[] args)
    {
        var showBanner = false;
        var showHelp = false;
        var showVersion = false;
        var variant = BannerVariantKind.Block;
        var noColor = false;
        var index = 0;

        while (index < args.Length)
        {
            ProcessFlag(args, ref index, ref showBanner, ref showHelp, ref showVersion, ref variant, ref noColor);
        }

        return new BannerCliResult(
            ShowBanner: showBanner,
            ShowHelp: showHelp,
            ShowVersion: showVersion,
            Variant: variant,
            NoColor: noColor);
    }

    /// <summary>
    ///     Inner switch extracted as a method so the outer loop's
    ///     <c>flag</c> variable can be declared once at the call site
    ///     without RCS1124 (Inline local variable).
    /// </summary>
    private static void ProcessFlag(
        string[] args,
        ref int index,
        ref bool showBanner,
        ref bool showHelp,
        ref bool showVersion,
        ref BannerVariantKind variant,
        ref bool noColor)
    {
        switch (args[index])
        {
            case "--banner":
            case "-b":
                showBanner = true;
                break;

            case "--help":
            case "-h":
            case "/?":
                showHelp = true;
                showBanner = true;
                break;

            case "--version":
            case "-v":
                showVersion = true;
                showBanner = true;
                break;

            case "--variant":
                if (index + 1 >= args.Length)
                {
                    return;
                }

                index++;
                var value = args[index];
                if (Enum.TryParse<BannerVariantKind>(value, ignoreCase: true, out var parsed))
                {
                    variant = parsed;
                }
                break;

            case "--no-color":
                noColor = true;
                break;
        }

        index++;
    }
}

/// <summary>
///     Parsed CLI flags relevant to the banner. Plain record so the
///     host and LoadGen can pattern-match on it without ceremony.
/// </summary>
public sealed record BannerCliResult(
    bool ShowBanner,
    bool ShowHelp,
    bool ShowVersion,
    BannerVariantKind Variant,
    bool NoColor);
