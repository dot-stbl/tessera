using System.DirectoryServices.Protocols;
using System.Globalization;
using System.Net;

namespace Tessera.Shared.Authentication.Providers.Ldap;

/// <summary>
///     Low-level LDAP roundtrip helper used by
///     <see cref="LdapAuthHandler" />. Extracted so the I/O can be
///     driven directly from unit tests without standing up the
///     framework authentication handler — the handler thin-wraps this
///     helper with HTTP plumbing. Lives in an <c>internal static class</c>
///     per <c>code-shape.md</c> §9 — pure logic with dependencies
///     passed in.
///     <para>
///         Returns the <see cref="LdapException" /> from any failure path
///         as <c>out</c> parameter rather than throwing, so the handler
///         can translate to a stable <see cref="LdapAuthHandler.ErrorCode" />
///         — exceptions vs. failed-auth decisions are different surfaces.
///     </para>
/// </summary>
internal static class LdapAuthenticationHelper
{
    /// <summary>
    ///     Performs the full search-and-bind dance against the
    ///     configured LDAP directory:
    ///     <list type="number">
    ///         <item>Bind with service account.</item>
    ///         <item>Search for the user by configured filter.</item>
    ///         <item>Bind with the user's DN + presented password.</item>
    ///     </list>
    /// </summary>
    /// <param name="options">Resolved LDAP options (incl. SecretReference-processed credentials).</param>
    /// <param name="username">Presented user identifier (substituted into <c>{0}</c> of the filter).</param>
    /// <param name="password">Presented password (used for the user bind).</param>
    /// <param name="userDn">On success, the matched user DN.</param>
    /// <param name="roles">On success, the role attribute values (may be empty).</param>
    /// <returns>
    ///     <c>null</c> on success; <see cref="LdapException" /> on any
    ///     binding/search/IO failure (network unreachable, invalid
    ///     credentials, malformed filter, etc.).
    /// </returns>
    public static LdapException? TryAuthenticate(
        LdapAuthOptions options,
        string username,
        string password,
        out string userDn,
        out List<string> roles)
    {
        userDn = "";
        roles = [];

        var identifier = CreateDirectoryIdentifier(options.Server);

        try
        {
            using var serviceConnection = new LdapConnection(identifier);
            if (IsSecureScheme(options.Server))
            {
                serviceConnection.SessionOptions.SecureSocketLayer = true;
            }

            serviceConnection.Bind(new NetworkCredential(options.BindDn, options.BindPassword));

            var filter = string.Format(
                CultureInfo.InvariantCulture,
                options.UserFilter,
                username);

            var searchRequest = new SearchRequest(
                options.BaseDn,
                filter,
                SearchScope.Subtree,
                ["dn", options.RoleAttribute]);

            var searchResponse = (SearchResponse)serviceConnection.SendRequest(searchRequest);

            if (searchResponse.Entries.Count == 0)
            {
                return null;
            }

            var entry = searchResponse.Entries[0];
            userDn = entry.DistinguishedName;
            roles = ExtractRoles(entry, options.RoleAttribute);

            using var userConnection = new LdapConnection(identifier);
            if (IsSecureScheme(options.Server))
            {
                userConnection.SessionOptions.SecureSocketLayer = true;
            }

            userConnection.Bind(new NetworkCredential(userDn, password));

            return null;
        }
        catch (LdapException ex)
        {
            return ex;
        }
    }

    private static LdapDirectoryIdentifier CreateDirectoryIdentifier(Uri server)
    {
        var host = server.Host;
        var port = server.IsDefaultPort ? 389 : server.Port;
        return new LdapDirectoryIdentifier(host, port);
    }

    /// <summary>
    ///     Returns <c>true</c> when the LDAP scheme is <c>ldaps://</c>
    ///     — in which case the handler should set
    ///     <see cref="LdapConnection.SessionOptions" />.
    ///     <c>SecureSocketLayer</c> to <c>true</c> after connection
    ///     construction.
    /// </summary>
    internal static bool IsSecureScheme(Uri server)
    {
        return string.Equals(server.Scheme, "ldaps", StringComparison.OrdinalIgnoreCase);
    }

    private static List<string> ExtractRoles(SearchResultEntry entry, string roleAttribute)
    {
        if (!entry.Attributes.Contains(roleAttribute))
        {
            return [];
        }

        var attribute = entry.Attributes[roleAttribute];
        var buffer = new List<string>();

        foreach (var value in attribute)
        {
            switch (value)
            {
                case string stringValue:
                    buffer.Add(stringValue);
                    break;
                case byte[] bytes:
                    buffer.Add(System.Text.Encoding.UTF8.GetString(bytes));
                    break;
            }
        }

        return buffer;
    }
}
