namespace Tessera.Host.Auth;

/// <summary>
///     Wire constants for the admin-bearer scheme — scheme name (as registered
///     by the host composition root and the principal authentication type)
///     and the standard <c>Authorization</c> header / bearer prefix. Lives in
///     a <c>file static class</c> so the auth handler doesn't carry
///     <c>private const string</c> alongside business behaviour
///     (constructors-and-fields.md §Constants rule).
/// </summary>
internal static class AdminBearerConstants
{
    public const string SchemeName = "admin";

    public const string AuthorizationHeader = "Authorization";

    public const string BearerPrefix = "Bearer ";
}
