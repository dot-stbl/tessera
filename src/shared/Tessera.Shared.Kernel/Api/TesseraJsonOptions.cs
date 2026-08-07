using System.Text.Json;
using System.Text.Json.Serialization;
using Tessera.Shared.Kernel.Identifiers.Json;

namespace Tessera.Shared.Kernel.Api;

/// <summary>
///     The single definition of Tessera's JSON wire format. Two pipelines
///     serialize responses — MVC (controller results) and raw
///     <c>System.Text.Json</c> (<c>TesseraExceptionHandler</c> writing a
///     <c>ProblemDetails</c> body) — and both take their rules from here, so a
///     format change cannot land in one and miss the other.
/// </summary>
public static class TesseraJsonOptions
{
    /// <summary>
    ///     Options for serializing outside MVC's pipeline. Same wire rules as
    ///     <see cref="ApplyTo" />, plus <c>WhenWritingNull</c>, which suits the
    ///     sparse ProblemDetails body this instance is used for.
    /// </summary>
    public static readonly JsonSerializerOptions Instance = TesseraJsonOptionsFactory.CreateInstance();

    /// <summary>
    ///     Applies Tessera's wire-format rules to an existing options object —
    ///     used to configure MVC, whose options are owned by the framework and
    ///     cannot be replaced wholesale.
    ///     <para>
    ///         The camelCase policy on <see cref="JsonStringEnumConverter" /> is
    ///         load-bearing, not cosmetic. Without it a C# enum member ships
    ///         verbatim (<c>"Ok"</c>, <c>"Healthy"</c>) while every TypeScript
    ///         consumer compares lowercase literals. Nothing throws — statuses
    ///         just never match, so error rows never highlight and health reads
    ///         as unknown.
    ///     </para>
    /// </summary>
    /// <param name="options">The options instance to configure in place.</param>
    public static void ApplyTo(JsonSerializerOptions options)
    {
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));

        // Identifier wrappers are a C# type-safety device, not part of the API.
        // Without these they serialize as { "value": "…" } and every consumer
        // has to unwrap an id that is a plain string everywhere else — in the
        // URL it came from, in the log record it correlates with.
        options.Converters.Add(new TraceIdJsonConverter());
        options.Converters.Add(new SpanIdJsonConverter());
    }
}

/// <summary>Factory for the shared <see cref="TesseraJsonOptions.Instance" />.</summary>
file static class TesseraJsonOptionsFactory
{
    public static JsonSerializerOptions CreateInstance()
    {
        var options = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        TesseraJsonOptions.ApplyTo(options);
        return options;
    }
}
