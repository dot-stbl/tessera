
using Tessera.Shared.Kernel.Results;
using Xunit;

namespace Tessera.Shared.Kernel.Tests.Results;
/// <summary>
///     Unit tests for <see cref="Result{T}" />.
/// </summary>
public sealed class ResultTests
{
    /// <summary>
    ///     <see cref="Result{T}.Ok" /> constructed with a value exposes that value via the
    ///     <see cref="Result{T}.Ok.Value" /> property.
    /// </summary>
    [Fact]
    public void Ok_ConstructedWithValue_ExposesValue()
    {
        var result = new Result<int>.Ok(42);

        Assert.Equal(42, result.Value);
    }

    /// <summary>
    ///     <see cref="Result{T}.Err" /> constructed with an <see cref="Error" /> exposes it via the
    ///     <see cref="Result{T}.Err.Error" /> property.
    /// </summary>
    [Fact]
    public void Err_ConstructedWithError_ExposesError()
    {
        var error = new Error("test.code", "test message");
        var result = new Result<int>.Err(error);

        Assert.Same(error, result.Error);
    }

    /// <summary>
    ///     Pattern matching on <see cref="Result{T}" /> selects the <see cref="Result{T}.Ok" />
    ///     branch and exposes the underlying value.
    /// </summary>
    [Fact]
    public void PatternMatching_OkBranch_ReturnsValue()
    {
        Result<int> result = new Result<int>.Ok(7);

        var extracted = result switch
        {
            Result<int>.Ok ok => ok.Value,
            Result<int>.Err => -1,
            _ => -2,
        };

        Assert.Equal(7, extracted);
    }

    /// <summary>
    ///     Pattern matching on <see cref="Result{T}" /> selects the <see cref="Result{T}.Err" />
    ///     branch and exposes the underlying error code.
    /// </summary>
    [Fact]
    public void PatternMatching_ErrBranch_ReturnsErrorCode()
    {
        Result<int> result = new Result<int>.Err(new Error("x", "y"));

        var code = result switch
        {
            Result<int>.Ok => "(ok)",
            Result<int>.Err err => err.Error.Code,
            _ => "(unknown)",
        };

        Assert.Equal("x", code);
    }
}