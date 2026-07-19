namespace Tessera.Shared.Kernel.Identifiers;

/// <summary>
/// Tenant identifier for multi-tenant deployments. MVP-01 is single-tenant;
/// this type exists for forward compatibility.
/// </summary>
public sealed record Tenant(string Value);