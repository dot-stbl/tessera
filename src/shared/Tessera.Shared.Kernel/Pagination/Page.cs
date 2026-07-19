namespace Tessera.Shared.Kernel.Pagination;

/// <summary>
///     Cursor-paginated result page. <see cref="Cursor" /> is opaque to clients;
///     pass it back to the originating endpoint unchanged to fetch the next page.
/// </summary>
/// <param name="Items">Items in this page.</param>
/// <param name="Cursor">Opaque pagination cursor, or null if no more pages.</param>
/// <param name="HasMore">True when more pages exist beyond this one.</param>
/// <typeparam name="T">Item type.</typeparam>
public sealed record Page<T>(IReadOnlyList<T> Items, string? Cursor, bool HasMore);
