namespace Tessera.Shared.Kernel.Domain.Spans;

/// <summary>
///     OpenTelemetry span kind (OTLP enum values). Distinguishes server/client
///     RPC, producer/consumer messaging, and internal work.
/// </summary>
public enum SpanKind
{
    /// <summary>Kind was not specified by the producer.</summary>
    Unspecified = 0,

    /// <summary>Internal operation within a service (default when omitted).</summary>
    Internal = 1,

    /// <summary>Inbound request handling (HTTP server, RPC server, …).</summary>
    Server = 2,

    /// <summary>Outbound request (HTTP client, RPC client, …).</summary>
    Client = 3,

    /// <summary>Message production (publish / send).</summary>
    Producer = 4,

    /// <summary>Message consumption (subscribe / receive).</summary>
    Consumer = 5,
}
