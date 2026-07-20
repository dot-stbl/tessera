using Tessera.Modules.Logs.Errors;

namespace Tessera.Modules.Logs.Unit.Errors;

/// <summary>
///     <see cref="LogsErrors" /> static contract test — the dot.case code
///     is consumed by FE on <c>problem.code</c>; rename is breaking.
/// </summary>
public sealed class LogsErrorsTests
{
    /// <summary>
    ///     <c>logs.trace_id_required</c> — emitted by <c>LogsController</c>
    ///     when the request omits <c>traceId</c>. MVP-01 restricts log
    ///     queries to trace-correlation lookups; ad-hoc LogsQL is MVP-02.
    /// </summary>
    [Fact]
    public void TraceIdRequired_IsStableDotCase()
    {
        Assert.Equal("logs.trace_id_required", LogsErrors.TraceIdRequired);
    }
}
