using System.Text;
using Tessera.Shared.Authentication.Admin;
using Xunit;

namespace Tessera.Host.Tests.Authentication;

/// <summary>
///     <see cref="AdminBearerCryptography.FixedTimeEquals" /> tests — the
///     constant-time comparison primitive that guards the admin-bearer
///     scheme against timing-attack token recovery. Any future change to
///     this helper MUST keep these properties intact.
/// </summary>
public sealed class AdminBearerCryptographyTests
{
    /// <summary>
    ///     Same-length equal inputs — must return true (sanity).
    /// </summary>
    [Fact]
    public void FixedTimeEquals_SameLengthIdenticalBytes_ReturnsTrue()
    {
        var left = Encoding.UTF8.GetBytes("equal-token");
        var right = Encoding.UTF8.GetBytes("equal-token");

        Assert.True(AdminBearerCryptography.FixedTimeEquals(left, right));
    }

    /// <summary>
    ///     Same-length but byte-mismatched inputs — must return false. The
    ///     iteration count is the same as the equal case (the loop
    ///     shouldn't short-circuit), guaranteeing constant-time behaviour
    ///     at the loop count level. The underlying
    ///     <see cref="System.Security.Cryptography.CryptographicOperations.FixedTimeEquals" />
    ///     enforces per-byte timing independence inside the loop.
    /// </summary>
    [Fact]
    public void FixedTimeEquals_SameLengthBytesDiffer_ReturnsFalse()
    {
        var left = Encoding.UTF8.GetBytes("token-AAAA");
        var right = Encoding.UTF8.GetBytes("token-BBBB");

        Assert.False(AdminBearerCryptography.FixedTimeEquals(left, right));
    }

    /// <summary>
    ///     Different-length inputs short-circuit to false without invoking
    ///     the inner loop. This is intentional: a side-channel on length
    ///     is preferable to a side-channel on per-byte mismatch position
    ///     (length is often already knowable from other request headers).
    /// </summary>
    [Fact]
    public void FixedTimeEquals_DifferentLengths_ReturnsFalse()
    {
        var left = Encoding.UTF8.GetBytes("short");
        var right = Encoding.UTF8.GetBytes("much-longer-input");

        Assert.False(AdminBearerCryptography.FixedTimeEquals(left, right));
    }

    /// <summary>
    ///     Empty spans on both sides — equal under the contract. Edge case
    ///     that becomes load-bearing when no token is configured (admin
    ///     scheme disabled, MVP-01 default).
    /// </summary>
    [Fact]
    public void FixedTimeEquals_BothEmpty_ReturnsTrue()
    {
        Assert.True(AdminBearerCryptography.FixedTimeEquals([], []));
    }
}
