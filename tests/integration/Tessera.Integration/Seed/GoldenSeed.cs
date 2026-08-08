namespace Tessera.Integration.Seed;

/// <summary>
///     Fixed fixture identity for wave-1 scenarios. Seed utilities must write
///     exactly these values so assertions stay deterministic.
/// </summary>
public static class GoldenSeed
{
    /// <summary>Service name used by the golden trace and logs.</summary>
    public const string ServiceName = "tessera-it-checkout";

    /// <summary>Root operation name on the golden trace.</summary>
    public const string RootOperation = "POST /it/checkout";

    /// <summary>
    ///     32-char hex trace id (OTel/Jaeger length). Seeder must emit this id.
    /// </summary>
    public const string TraceId = "a1b2c3d4e5f6789012345678abcdef01";

    /// <summary>Root span id (16-char hex).</summary>
    public const string RootSpanId = "0123456789abcdef";

    /// <summary>Child CLIENT span that fails (for errors wave).</summary>
    public const string ErrorSpanId = "fedcba9876543210";
}
