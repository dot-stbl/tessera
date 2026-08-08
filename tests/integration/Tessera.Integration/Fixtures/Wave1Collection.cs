using Tessera.Integration.Seed;
using Tessera.Integration.Support;
using Xunit;

namespace Tessera.Integration.Fixtures;

/// <summary>xUnit collection name for wave-1 stack + host fixture.</summary>
public static class Wave1CollectionNames
{
    /// <summary>Shared collection for seed + host scenarios.</summary>
    public const string Name = "wave1-stack-host";
}

/// <summary>
///     One shared stack seed + host process per collection (when
///     <see cref="IntegrationGate.IsEnabled" />).
/// </summary>
public sealed class Wave1Fixture : IAsyncLifetime
{
    /// <summary>Host process when IT is enabled; null when gate closed.</summary>
    public TesseraHostProcess? Host { get; private set; }

    /// <summary>Client against <see cref="Host" />; null when gate closed.</summary>
    public HttpClient? HostClient { get; private set; }

    /// <summary>UTC window covering the seeded golden span (ms).</summary>
    public long WindowStartUnixMs { get; private set; }

    /// <summary>UTC window end for search queries (ms).</summary>
    public long WindowEndUnixMs { get; private set; }

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        if (!IntegrationGate.IsEnabled)
        {
            return;
        }

        WindowEndUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 60_000;
        WindowStartUnixMs = WindowEndUnixMs - 24 * 60 * 60 * 1000;

        using (var stackHttp = new HttpClient { Timeout = TimeSpan.FromSeconds(15) })
        {
            var seeder = new GoldenSeeder(stackHttp);
            await seeder.SeedAsync();
        }

        // VT/VL may need a moment to index before select APIs see the write.
        await Task.Delay(1500);

        Host = await TesseraHostProcess.StartAsync();
        HostClient = Host.CreateClient();
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        HostClient?.Dispose();
        HostClient = null;
        if (Host is not null)
        {
            await Host.DisposeAsync();
            Host = null;
        }
    }
}

/// <summary>Binds <see cref="Wave1Fixture" /> as a collection fixture.</summary>
[CollectionDefinition(Wave1CollectionNames.Name)]
public sealed class Wave1CollectionDefinition : ICollectionFixture<Wave1Fixture>;
