using Tessera.Shared.Kernel.Exceptions;
using Tessera.Shared.Web.Errors;
using Xunit;

namespace Tessera.Host.Tests.Errors;

/// <summary>
///     Static mapping helper <see cref="TesseraExceptionHandlerHelpers.MapStatus" />
///     tests — verifies the typed-exception → HTTP status table used by the
///     global <see cref="TesseraExceptionHandler" />. Each branch must map
///     to exactly one status; any future error subtype must add a test here
///     before its handler change is considered complete.
/// </summary>
public sealed class TesseraExceptionHandlerHelpersTests
{
    /// <summary>
    ///     <see cref="ProviderNotFoundException" /> is the typed "the
    ///     upstream has no record under that id" signal. Maps to 404 so
    ///     the global handler writes a ProblemDetails body with
    ///     <c>type=/errors/...</c> rather than treating upstream absence as
    ///     a generic 5xx.
    /// </summary>
    [Fact]
    public void MapStatus_ProviderNotFoundException_Returns404()
    {
        var exception = new ProviderNotFoundException("trace.not_found", "trace 0x... not found");

        Assert.Equal(404, TesseraExceptionHandlerHelpers.MapStatus(exception));
    }

    /// <summary>
    ///     <see cref="ProviderTimeoutException" /> maps to 504 — separate
    ///     from generic 502 because timeouts are operationally distinct
    ///     from network failures (the upstream accepted the connection but
    ///     didn't respond within budget).
    /// </summary>
    [Fact]
    public void MapStatus_ProviderTimeoutException_Returns504()
    {
        var exception = new ProviderTimeoutException("provider.timeout", "upstream stalled");

        Assert.Equal(504, TesseraExceptionHandlerHelpers.MapStatus(exception));
    }

    /// <summary>
    ///     Base <see cref="ProviderException" /> — used for transient
    ///     network errors, 5xx-from-upstream, mapping failures. Maps to 502
    ///     so the FE knows the request was syntactically fine but the
    ///     backend didn't reach the upstream successfully.
    /// </summary>
    [Fact]
    public void MapStatus_BaseProviderException_Returns502()
    {
        var exception = new ProviderException("provider.network_error", "connection refused");

        Assert.Equal(502, TesseraExceptionHandlerHelpers.MapStatus(exception));
    }

    /// <summary>
    ///     Subclass precedence: <see cref="ProviderNotFoundException" />
    ///     inherits from <see cref="ProviderException" /> but must take the
    ///     404 branch — the <c>switch</c> expression in
    ///     <see cref="TesseraExceptionHandlerHelpers.MapStatus" /> orders
    ///     subtypes first. Guards against reordering that would silently
    ///     give a 502 for what should be a 404.
    /// </summary>
    [Fact]
    public void MapStatus_ProviderNotFoundSubclassBeatsBaseType()
    {
        ProviderException asBase = new ProviderNotFoundException("trace.not_found", "missing");

        Assert.Equal(404, TesseraExceptionHandlerHelpers.MapStatus(asBase));
    }
}
