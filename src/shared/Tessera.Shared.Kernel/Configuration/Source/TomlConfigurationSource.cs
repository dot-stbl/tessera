using Microsoft.Extensions.Configuration;

namespace Tessera.Shared.Kernel.Configuration;

/// <summary>
///     Source descriptor for <see cref="TomlConfigurationProvider" />. The
///     base <see cref="FileConfigurationSource" /> supplies <c>Path</c>,
///     <c>Optional</c>, <c>ReloadOnChange</c>, <c>ReloadDelay</c>; this class
///     just locks the provider type so <see cref="IConfigurationBuilder" />
///     can find it via reflection.
/// </summary>
public sealed class TomlConfigurationSource : FileConfigurationSource
{
    /// <inheritdoc />
    public override IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        EnsureDefaults(builder);
        return new TomlConfigurationProvider(this);
    }
}