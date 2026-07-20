using Tessera.Modules.Traces.Errors;

namespace Tessera.Modules.Traces.Unit.Errors;

/// <summary>
///     <see cref="TracesErrors" /> static contract tests — error codes
///     surface as <c>problem.code</c> values in RFC 9457 responses, so any
///     rename is a breaking change for FE consumers. Lock them.
/// </summary>
public sealed class TracesErrorsTests
{
    /// <summary>
    ///     <c>trace.not_found</c> — emitted by <c>TracesController.GetAsync</c>
    ///     when upstream <see cref="Tessera.Shared.Kernel.Providers.Traces.ITraceProvider.GetByIdAsync" />
    ///     returns null. Mapped to HTTP 404 by the global IExceptionHandler.
    /// </summary>
    [Fact]
    public void TraceNotFound_IsStableDotCase()
    {
        Assert.Equal("trace.not_found", TracesErrors.TraceNotFound);
    }

    /// <summary>
    ///     <c>trace.search.invalid</c> — reserved for invalid query shape
    ///     (negative duration, end-before-start, etc.); not currently
    ///     emitted by MVP-01 controller code but reserved for Phase 5+.
    /// </summary>
    [Fact]
    public void SearchInvalid_IsStableDotCase()
    {
        Assert.Equal("trace.search.invalid", TracesErrors.SearchInvalid);
    }

    /// <summary>
    ///     <c>provider.network_error</c> — upstream generic network failure
    ///     for non-typed <see cref="Tessera.Shared.Kernel.Exceptions.ProviderException" />;
    ///     mapper at host root maps to HTTP 502.
    /// </summary>
    [Fact]
    public void BackendUnreachable_IsStableDotCase()
    {
        Assert.Equal("provider.network_error", TracesErrors.BackendUnreachable);
    }
}
