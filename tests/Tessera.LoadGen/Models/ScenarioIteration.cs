namespace Tessera.LoadGen.Models;

/// <summary>
///     Per-request context shared by every scenario implementation —
///     the synthesised services, the active <see cref="System.Diagnostics.ActivitySource" />,
///     and the active <see cref="Microsoft.Extensions.Logging.ILogger" />.
///     Scenario implementations read these to emit spans, logs, and
///     tags without re-plumbing.
/// </summary>
public sealed record ScenarioIteration(
    SyntheticService Service,
    string Operation,
    System.Diagnostics.ActivitySource ActivitySource,
    Microsoft.Extensions.Logging.ILogger Logger,
    Random Random);
