using System.Net;
using System.Text.Json;
using Tessera.Integration.Fixtures;
using Tessera.Integration.Seed;
using Tessera.Integration.Support;
using Xunit;

namespace Tessera.Integration.Scenarios;

/// <summary>Wave-1: list + get-trace against golden seed.</summary>
[Collection(Wave1CollectionNames.Name)]
public sealed class TracesSearchScenario(Wave1Fixture fixture)
{
    /// <summary>
    ///     Search by service includes <see cref="GoldenSeed.TraceId" />.
    /// </summary>
    [Fact]
    public async Task ListTraces_ByService_ContainsGoldenTraceId()
    {
        if (!IntegrationGate.IsEnabled || fixture.HostClient is null)
        {
            return;
        }

        var url =
            $"api/v1/traces?startUnixMs={fixture.WindowStartUnixMs}&endUnixMs={fixture.WindowEndUnixMs}"
            + $"&service={Uri.EscapeDataString(GoldenSeed.ServiceName)}&limit=50";

        using var response = await fixture.HostClient.GetAsync(url);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.IsSuccessStatusCode,
            $"list traces {(int)response.StatusCode}: {body}");

        Assert.Contains(GoldenSeed.TraceId, body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Get-trace returns Full or SpansOnly when VL correlation works / fails.
    /// </summary>
    [Fact]
    public async Task GetTrace_ById_ReturnsSpansWithOptionalLogs()
    {
        if (!IntegrationGate.IsEnabled || fixture.HostClient is null)
        {
            return;
        }

        using var response = await fixture.HostClient.GetAsync($"api/v1/traces/{GoldenSeed.TraceId}");
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode is HttpStatusCode.OK,
            $"get trace {(int)response.StatusCode}: {body}");

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        Assert.True(root.TryGetProperty("mode", out var modeElement), body);
        var mode = modeElement.GetString();
        // Wire enums are camelCase (TesseraJsonOptions).
        Assert.True(
            mode is "full" or "spansOnly",
            $"expected full or spansOnly, got {mode}: {body}");

        Assert.True(root.TryGetProperty("trace", out var traceElement), body);
        Assert.Equal(JsonValueKind.Object, traceElement.ValueKind);

        if (mode == "full")
        {
            Assert.True(root.TryGetProperty("correlatedLogs", out var logs), body);
            Assert.True(logs.GetArrayLength() >= 1, "full mode should include correlated logs: " + body);
        }
    }
}
