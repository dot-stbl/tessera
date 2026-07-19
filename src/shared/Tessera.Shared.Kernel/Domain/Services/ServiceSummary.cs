namespace Tessera.Shared.Kernel.Domain.Services;

/// <summary>
///     Service inventory summary with aggregated metrics across traces.
/// </summary>
public sealed record ServiceSummary(
    string Name,
    int SpanCount,
    int ErrorCount,
    IReadOnlyList<ServiceOperation> Operations);
