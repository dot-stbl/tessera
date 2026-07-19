
using Tessera.Shared.Kernel.Pagination;
using Xunit;

namespace Tessera.Shared.Kernel.Tests.Pagination;
/// <summary>
///     Unit tests for <see cref="Page{T}" />.
/// </summary>
public sealed class PageTests
{
    private static readonly int[] Items = [1, 2, 3];

    private static readonly string[] StringItems = ["a", "b"];

    /// <summary>
    ///     <see cref="Page{T}" /> stores items, cursor, and has-more flag as properties.
    /// </summary>
    [Fact]
    public void Ctor_ItemsCursorHasMore_StoredAsProperties()
    {
        var page = new Page<int>(Items, "next-cursor", true);

        Assert.Same(Items, page.Items);
        Assert.Equal("next-cursor", page.Cursor);
        Assert.True(page.HasMore);
    }

    /// <summary>
    ///     <see cref="Page{T}" /> constructed for a terminal page has null cursor and
    ///     <c>HasMore = false</c>.
    /// </summary>
    [Fact]
    public void Ctor_NoMorePages_CursorNullHasMoreFalse()
    {
        var page = new Page<int>(Array.Empty<int>(), null, false);

        Assert.Null(page.Cursor);
        Assert.False(page.HasMore);
        Assert.Empty(page.Items);
    }

    /// <summary>
    ///     <see cref="Page{T}" /> is generic and works for non-int element types.
    /// </summary>
    [Fact]
    public void Ctor_GenericOverString_StoresItemsAsStrings()
    {
        var page = new Page<string>(StringItems, "c", true);

        Assert.Equal("a", page.Items[0]);
        Assert.Equal("b", page.Items[1]);
    }
}