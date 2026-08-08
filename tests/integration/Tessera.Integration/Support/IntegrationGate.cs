namespace Tessera.Integration.Support;

/// <summary>
///     Opt-in gate for expensive integration tests. Without
///     <c>TESSERA_IT=1</c> (or <c>true</c>/<c>yes</c>), scenarios soft-return so
///     laptops without podman stay green.
/// </summary>
public static class IntegrationGate
{
    /// <summary>Environment variable that enables the integration suite.</summary>
    public const string EnvName = "TESSERA_IT";

    /// <summary>True when the operator opted into integration runs.</summary>
    public static bool IsEnabled
    {
        get
        {
            var value = Environment.GetEnvironmentVariable(EnvName);
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return value is "1" or "true" or "TRUE" or "yes" or "YES";
        }
    }

    /// <summary>xUnit Skip reason when the gate is closed.</summary>
    public const string SkipReason =
        "Set TESSERA_IT=1 and podman compose -f tests/integration/stack/docker-compose.yml up -d";
}
