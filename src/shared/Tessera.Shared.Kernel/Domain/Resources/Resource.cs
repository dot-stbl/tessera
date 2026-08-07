namespace Tessera.Shared.Kernel.Domain.Resources;

/// <summary>
///     OpenTelemetry resource describing the entity that produced telemetry
///     (typically a process / service instance). Small first-class fields for
///     the common service identity attributes; remaining resource attributes
///     live in <see cref="Attributes" /> for project predicates and display.
/// </summary>
/// <param name="ServiceName">
///     <c>service.name</c> — required service identity (never empty in well-formed data).
/// </param>
/// <param name="ServiceNamespace">Optional <c>service.namespace</c>.</param>
/// <param name="DeploymentEnvironment">Optional <c>deployment.environment</c>.</param>
/// <param name="Attributes">Remaining resource attributes (open dict).</param>
public sealed record Resource(
    string ServiceName,
    string? ServiceNamespace,
    string? DeploymentEnvironment,
    IReadOnlyDictionary<string, string> Attributes);
