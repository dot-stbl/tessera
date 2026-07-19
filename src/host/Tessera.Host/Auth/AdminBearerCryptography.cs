using System.Security.Cryptography;

namespace Tessera.Host.Auth;

/// <summary>
///     Length-equalizing constant-time byte comparison for admin-bearer token
///     validation. The shorter input is implicitly extended to the length of
///     the longer so the loop runs the same number of iterations regardless
///     of where the bytes first differ — closes the timing side channel on
///     token length. Lives in a <c>file static class</c> so the handler itself
///     stays focused on the request lifecycle (code-shape.md §9 ban).
/// </summary>
internal static class AdminBearerCryptography
{
    public static bool FixedTimeEquals(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(left, right);
    }
}
