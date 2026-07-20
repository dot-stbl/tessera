using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Tessera.Shared.Kernel.Configuration.Layout;

/// <summary>
///     Resolves the <see cref="IFileSystemLayout" /> implementation at
///     composition-root time. Default = auto-detect via
///     <see cref="OperatingSystem.IsLinux()" /> / <c>IsWindows()</c>.
///     Override via the <c>[fs_layout]/provider</c> TOML section or the
///     <c>TESSERA_FS_LAYOUT_PROVIDER</c> env var. Per ADR-0001 Decision 7.
/// </summary>
/// <remarks>
///     <para>
///         Today two providers ship: <see cref="LinuxFileSystemLayout" />
///         and <see cref="WindowsFileSystemLayout" />. macOS uses the
///         Linux variant via its BSD-style paths (XDG-equivalent under
///         <c>~/.config</c> / <c>~/.local/share</c>).
///     </para>
/// </remarks>
public static class FileSystemLayoutProvider
{
    /// <summary>Default auto-detection. Linux hosts get Linux layout,
    /// Windows hosts get Windows layout, macOS falls through to Linux
    /// (XDG-style paths in <c>~/.config</c>).</summary>
    public static IFileSystemLayout Detect()
    {
        if (OperatingSystem.IsWindows())
        {
            return new WindowsFileSystemLayout();
        }

        return new LinuxFileSystemLayout();
    }

    /// <summary>
    ///     Force-resolve based on a configured provider name. Useful in
    ///     tests + cross-platform deployment scripts that want to pin the
    ///     layout regardless of host OS.
    /// </summary>
    /// <exception cref="ArgumentException"></exception>
    public static IFileSystemLayout Resolve(string? providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName) || providerName == "auto")
        {
            return Detect();
        }

        return providerName switch
        {
            "linux" => new LinuxFileSystemLayout(),
            "windows" => new WindowsFileSystemLayout(),
            _ => throw new ArgumentException(
                $"Unknown fs_layout provider '{providerName}'. Supported: 'auto', 'linux', 'windows'.",
                nameof(providerName)),
        };
    }
}

/// <summary>
///     TOML-bindable options for <see cref="FileSystemLayoutProvider" />.
///     Bound from the <c>[fs_layout]</c> section of <c>tessera.toml</c>.
///     <c>provider = "auto"</c> is the default — pin
///     <c>"linux"</c> or <c>"windows"</c> only in tests or in a
///     deployment that wants layout-independent of OS detection.
/// </summary>
public sealed class FileSystemLayoutOptions
{
    /// <summary>Configuration section name in <c>tessera.toml</c>.</summary>
    public const string SectionName = "fs_layout";

    /// <summary>
    ///     Provider name. <c>"auto"</c> (default) detects via
    ///     <see cref="OperatingSystem" />. <c>"linux"</c> and
    ///     <c>"windows"</c> force the named implementation.
    /// </summary>
    public string Provider { get; init; } = "auto";
}

/// <summary>
///     <see cref="IConfigureOptions{TOptions}" /> that reads
///     <c>TESSERA_FS_LAYOUT_PROVIDER</c> env var as a fallback override
///     when the TOML section is absent. Binds <see cref="FileSystemLayoutOptions" />
///     in DI for pipeline composition.
/// </summary>
public static class FileSystemLayoutInstallerExtensions
{
    /// <summary>Environment variable name for inline provider override.</summary>
    public const string ProviderEnv = "TESSERA_FS_LAYOUT_PROVIDER";

    /// <summary>
    ///     Register <see cref="FileSystemLayoutOptions" /> + bind the singleton
    ///     <see cref="IFileSystemLayout" /> resolved via
    ///     <see cref="FileSystemLayoutProvider.Resolve(string?)" />. Call after
    ///     <c>AddTesseraConfiguration</c> so the <c>[fs_layout]</c> section is
    ///     available.
    /// </summary>
    public static IServiceCollection AddTesseraFileSystemLayout(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<FileSystemLayoutOptions>()
            .Bind(configuration.GetSection(FileSystemLayoutOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Env override takes precedence over TOML via the env check inside
        // the factory delegate — file_layout env var is a deploy-time
        // orchestrator hook (per ADR-0001 Decision 7 override mechanism).
        services.AddSingleton<IFileSystemLayout>(static sp =>
        {
            var options = sp.GetRequiredService<IOptions<FileSystemLayoutOptions>>().Value;
            var envOverride = Environment.GetEnvironmentVariable(ProviderEnv);
            return FileSystemLayoutProvider.Resolve(envOverride ?? options.Provider);
        });

        return services;
    }
}
