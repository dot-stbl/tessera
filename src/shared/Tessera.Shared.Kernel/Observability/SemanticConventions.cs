namespace Tessera.Shared.Kernel.Observability;

/// <summary>
///     Vendored OpenTelemetry semantic-convention attribute keys used by Tessera
///     domain mapping and derivation. Mirrors OTel semconv 1.43.0 — not the
///     <c>OpenTelemetry.SemanticConventions</c> package (stability / Kernel leaf
///     dependency concerns; see ADR-0002).
/// </summary>
public static class SemanticConventions
{
    /// <summary>Logical service name (<c>service.name</c>).</summary>
    public const string ServiceName = "service.name";

    /// <summary>Service namespace (<c>service.namespace</c>).</summary>
    public const string ServiceNamespace = "service.namespace";

    /// <summary>Deployment environment name (<c>deployment.environment</c>).</summary>
    public const string DeploymentEnvironment = "deployment.environment";

    /// <summary>Span kind attribute when carried as a tag (<c>span.kind</c>).</summary>
    public const string SpanKind = "span.kind";

    /// <summary>OTel status code tag (<c>otel.status_code</c>).</summary>
    public const string OtelStatusCode = "otel.status_code";

    /// <summary>HTTP response status code (<c>http.response.status_code</c>).</summary>
    public const string HttpResponseStatusCode = "http.response.status_code";

    /// <summary>gRPC status code (<c>rpc.grpc.status_code</c>).</summary>
    public const string RpcGrpcStatusCode = "rpc.grpc.status_code";

    /// <summary>Exception type name (<c>exception.type</c>).</summary>
    public const string ExceptionType = "exception.type";

    /// <summary>Exception message (<c>exception.message</c>).</summary>
    public const string ExceptionMessage = "exception.message";

    /// <summary>Exception stacktrace (<c>exception.stacktrace</c>).</summary>
    public const string ExceptionStacktrace = "exception.stacktrace";

    /// <summary>Database system identifier (<c>db.system</c>).</summary>
    public const string DbSystem = "db.system";

    /// <summary>Remote service name (<c>peer.service</c>).</summary>
    public const string PeerService = "peer.service";

    /// <summary>Server address (<c>server.address</c>).</summary>
    public const string ServerAddress = "server.address";

    /// <summary>Messaging system name (<c>messaging.system</c>).</summary>
    public const string MessagingSystem = "messaging.system";

    /// <summary>Messaging destination name (<c>messaging.destination.name</c>).</summary>
    public const string MessagingDestinationName = "messaging.destination.name";
}
