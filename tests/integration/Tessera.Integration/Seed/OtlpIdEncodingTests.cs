using Xunit;

namespace Tessera.Integration.Seed;

/// <summary>Pure unit checks for seed URL/id helpers (always run).</summary>
public sealed class OtlpIdEncodingTests
{
    /// <summary>Known 16-byte hex maps to stable base64 (OTLP JSON bytes).</summary>
    [Fact]
    public void HexToBase64_RootSpanId_IsStable()
    {
        // 0123456789abcdef → 8 bytes
        var actual = OtlpIdEncoding.HexToBase64(GoldenSeed.RootSpanId);
        Assert.Equal(Convert.ToBase64String(Convert.FromHexString(GoldenSeed.RootSpanId)), actual);
    }

    /// <summary>Trace id (16 bytes) maps to base64 without padding issues for VT.</summary>
    [Fact]
    public void HexToBase64_TraceId_RoundTripsBytes()
    {
        var b64 = OtlpIdEncoding.HexToBase64(GoldenSeed.TraceId);
        var bytes = Convert.FromBase64String(b64);
        Assert.Equal(GoldenSeed.TraceId, Convert.ToHexString(bytes).ToLowerInvariant());
    }

    /// <summary>Insert URLs compose without double slashes.</summary>
    [Theory]
    [InlineData("http://127.0.0.1:10428", "http://127.0.0.1:10428/insert/opentelemetry/v1/traces")]
    [InlineData("http://127.0.0.1:10428/", "http://127.0.0.1:10428/insert/opentelemetry/v1/traces")]
    public void TracesOtlpInsertUrl_NormalizesBase(string baseUrl, string expected)
    {
        Assert.Equal(expected, OtlpIdEncoding.TracesOtlpInsertUrl(baseUrl));
    }

    /// <summary>VL fallback path is documented jsonline insert.</summary>
    [Fact]
    public void LogsJsonLineInsertUrl_EndsWithJsonline()
    {
        Assert.Equal(
            "http://127.0.0.1:9428/insert/jsonline",
            OtlpIdEncoding.LogsJsonLineInsertUrl("http://127.0.0.1:9428"));
    }
}
