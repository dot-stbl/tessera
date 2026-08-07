using System.Text.Json;
using Tessera.Shared.Kernel.Api;
using Tessera.Shared.Kernel.Domain.Spans;
using Tessera.Shared.Kernel.Domain.Traces;
using Tessera.Shared.Kernel.Providers.Health;
using Tessera.Shared.Kernel.Time;
using Xunit;

namespace Tessera.Shared.Unit.Api;

/// <summary>
///     Wire-format lock for <see cref="TesseraJsonOptions" />. Every enum that
///     crosses the HTTP boundary is asserted by its serialized spelling, because
///     the failure mode this guards against is silent: a wrong casing policy
///     ships <c>"Ok"</c> instead of <c>"ok"</c>, no exception is raised anywhere,
///     and the only symptom is that the UI stops highlighting errors. A test is
///     the only place that failure becomes loud.
/// </summary>
public sealed class TesseraJsonOptionsTests
{
    private sealed record StatusEnvelope(TraceStatus Status);

    private sealed record LevelEnvelope(LogLevel Level);

    private sealed record HealthEnvelope(HealthStatus Status);

    private sealed record KindEnvelope(SpanKind Kind);

    private sealed record CasingEnvelope(string RootOperation, long DurationMs);

    /// <summary>
    ///     The three <see cref="TraceStatus" /> members the FE's status union is
    ///     built from. Single-word members are the dangerous case: they look
    ///     right in C# and wrong on the wire.
    /// </summary>
    [Theory]
    [InlineData(TraceStatus.Ok, "ok")]
    [InlineData(TraceStatus.Error, "error")]
    [InlineData(TraceStatus.Unset, "unset")]
    public void Serialize_TraceStatus_UsesLowerCamelCase(TraceStatus status, string expected)
    {
        var json = JsonSerializer.Serialize(new StatusEnvelope(status), TesseraJsonOptions.Instance);

        Assert.Equal($"{{\"status\":\"{expected}\"}}", json);
    }

    /// <summary>
    ///     Log severities travel by name and the FE composes a CSS class from
    ///     that name (<c>log-level-{level}</c>), so an unexpected spelling
    ///     renders an unstyled chip rather than failing. Note <c>info</c> and
    ///     <c>warn</c>: the members are deliberately named for OTel's
    ///     SeverityText, not .NET's Information/Warning.
    /// </summary>
    [Theory]
    [InlineData(LogLevel.Trace, "trace")]
    [InlineData(LogLevel.Debug, "debug")]
    [InlineData(LogLevel.Info, "info")]
    [InlineData(LogLevel.Warn, "warn")]
    [InlineData(LogLevel.Error, "error")]
    [InlineData(LogLevel.Fatal, "fatal")]
    public void Serialize_LogLevel_UsesOtelSeverityNames(LogLevel level, string expected)
    {
        var json = JsonSerializer.Serialize(new LevelEnvelope(level), TesseraJsonOptions.Instance);

        Assert.Equal($"{{\"level\":\"{expected}\"}}", json);
    }

    /// <summary>
    ///     Health drives a single indicator in the app shell; a casing mismatch
    ///     there means the indicator never leaves its unknown state.
    /// </summary>
    [Theory]
    [InlineData(HealthStatus.Healthy, "healthy")]
    [InlineData(HealthStatus.Degraded, "degraded")]
    [InlineData(HealthStatus.Unhealthy, "unhealthy")]
    public void Serialize_HealthStatus_UsesLowerCamelCase(HealthStatus status, string expected)
    {
        var json = JsonSerializer.Serialize(new HealthEnvelope(status), TesseraJsonOptions.Instance);

        Assert.Equal($"{{\"status\":\"{expected}\"}}", json);
    }

    /// <summary>
    ///     <see cref="SpanKind" /> is the OTel span kind; the FE keys client /
    ///     server icons off it.
    /// </summary>
    [Theory]
    [InlineData(SpanKind.Unspecified, "unspecified")]
    [InlineData(SpanKind.Internal, "internal")]
    [InlineData(SpanKind.Server, "server")]
    [InlineData(SpanKind.Client, "client")]
    [InlineData(SpanKind.Producer, "producer")]
    [InlineData(SpanKind.Consumer, "consumer")]
    public void Serialize_SpanKind_UsesLowerCamelCase(SpanKind kind, string expected)
    {
        var json = JsonSerializer.Serialize(new KindEnvelope(kind), TesseraJsonOptions.Instance);

        Assert.Equal($"{{\"kind\":\"{expected}\"}}", json);
    }

    /// <summary>
    ///     Property names are camelCase, including the acronym-free compound
    ///     names the generated TypeScript will mirror.
    /// </summary>
    [Fact]
    public void Serialize_PropertyNames_AreCamelCase()
    {
        var json = JsonSerializer.Serialize(new CasingEnvelope("POST /checkout", 1250), TesseraJsonOptions.Instance);

        Assert.Equal(/*lang=json,strict*/ "{\"rootOperation\":\"POST /checkout\",\"durationMs\":1250}", json);
    }

    /// <summary>
    ///     <see cref="TesseraJsonOptions.ApplyTo" /> is what configures MVC, so
    ///     it must produce the same wire format as
    ///     <see cref="TesseraJsonOptions.Instance" />. This is the assertion that
    ///     keeps the two serialization pipelines from drifting apart.
    /// </summary>
    [Fact]
    public void ApplyTo_ProducesSameWireFormatAsSharedInstance()
    {
        var mvcLike = new JsonSerializerOptions();
        TesseraJsonOptions.ApplyTo(mvcLike);

        var viaApplyTo = JsonSerializer.Serialize(new StatusEnvelope(TraceStatus.Error), mvcLike);
        var viaInstance = JsonSerializer.Serialize(new StatusEnvelope(TraceStatus.Error), TesseraJsonOptions.Instance);

        Assert.Equal(viaInstance, viaApplyTo);
        Assert.Equal(/*lang=json,strict*/ "{\"status\":\"error\"}", viaApplyTo);
    }
}
