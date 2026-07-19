namespace Tessera.Providers.Victoria.Dto.Jaeger.Trace;

/// <summary>
///     Service / process metadata referenced by Jaeger spans via <c>processID</c>.
///     Holds the canonical <c>serviceName</c> plus any process-level tags
///     (typically OTel resource attributes like <c>service.namespace</c>).
/// </summary>
public sealed record JaegerProcess(
    string ServiceName,
    IReadOnlyList<JaegerTag> Tags);
