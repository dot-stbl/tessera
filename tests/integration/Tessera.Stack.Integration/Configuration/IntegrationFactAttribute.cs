namespace Tessera.Stack.Integration.Configuration;

/// <summary>
///     xUnit <see cref="FactAttribute" /> that self-skips integration
///     tests unless <c>TESSERA_INTEGRATION=1</c> is present in the
///     environment. Keeps <c>dotnet test</c> green in the default
///     CI / developer run while still letting the tests be exercised
///     when the user has the compose stack up.
/// </summary>
/// <remarks>
///     <para>
///         <b>Skipped, not failed.</b> xunit reports skipped tests
///         with the <c>Skip</c> reason — they're not counted as
///         failures by the runner but they do show up as skipped, so
///         reviewers can see the test surface exists without taking
///         action.
///     </para>
///     <para>
///         <b>Enabling locally.</b>
///         <code>
///           TESSERA_INTEGRATION=1 dotnet test tests/integration/Tessera.Stack.Integration
///         </code>
///         Tests still need the compose stack running independently:
///         <c>docker compose -f compose/docker-compose.yml up -d
///         victoria-metrics victoria-logs victoria-traces
///         otel-collector</c>.
///     </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class IntegrationFactAttribute : FactAttribute
{
    public IntegrationFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("TESSERA_INTEGRATION") != "1")
        {
            Skip = "Integration tests require TESSERA_INTEGRATION=1 and a running compose stack (see compose/README.md).";
        }
    }
}
