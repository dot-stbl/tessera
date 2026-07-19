using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tessera.Shared.Kernel.Api;

/// <summary>
///     Shared <see cref="JsonSerializerOptions" /> used wherever
///     <c>System.Text.Json</c> serializes outside of MVC's default pipeline —
///     e.g. <c>TesseraExceptionHandler</c> writing a <c>ProblemDetails</c>
///     body to the response. Centralizing prevents per-call-site allocation of
///     a fresh <see cref="JsonSerializerOptions" /> and keeps wire format
///     consistent across the app.
/// </summary>
public static class TesseraJsonOptions
{
    /// <summary>
    ///     The shared options instance: enum-as-string via
    ///     <see cref="JsonStringEnumConverter" />, camelCase property names
    ///     to match the convention the FE codegen pipeline expects. Read-only
    ///     after first initialization.
    /// </summary>
    public static readonly JsonSerializerOptions Instance = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}