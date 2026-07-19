namespace Tessera.Shared.Kernel.Domain.Services;

/// <summary>
///     A single operation (e.g. endpoint or RPC method) belonging to a service.
/// </summary>
public sealed record ServiceOperation(
    string Name,
    int Count);
