namespace Tessera.Shared.Kernel.Results;

/// <summary>
///     Discriminated union for fallible operations. Match via pattern:
///     <code>
/// return result switch
/// {
///     Result&lt;T&gt;.Ok ok =&gt; ok.Value,
///     Result&lt;T&gt;.Err err =&gt; default,
/// };
/// </code>
/// </summary>
/// <typeparam name="T">Success value type.</typeparam>
public abstract record Result<T>
{
    /// <summary>Successful result carrying a value.</summary>
    public sealed record Ok(T Value) : Result<T>;

    /// <summary>Failed result carrying a structured error.</summary>
    public sealed record Err(Error Error) : Result<T>;
}
