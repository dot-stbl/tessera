namespace Tessera.Shared.Kernel.Providers.Health;

/// <summary>
/// Abstraction over a provider's health probe. Each backend (VT/VL/VM/etc.)
/// implements this to expose its connectivity status.
/// </summary>
public interface IHealthProvider
{
    /// <summary>
    /// Probe the backend. Returns status + optional detail message.
    /// </summary>
    public Task<ProviderHealthReport> CheckAsync(CancellationToken ct);
}