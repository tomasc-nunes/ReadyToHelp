namespace readytohelpapi.Authentication.Service;

using readytohelpapi.User.Models;
using readytohelpapi.Authentication.Models;

/// <summary>
/// Defines the contract for issuing, validating, and revoking authentication sessions.
/// </summary>
public interface IAuthSessionStore
{
    /// <summary>
    /// Creates a session or replaces the token of an existing valid session without extending its expiration.
    /// Rejects renewal when the session, previous token, or account details are no longer valid.
    /// </summary>
    /// <param name="user">The user for whom the session is issued.</param>
    /// <param name="tokenId">The identifier of the new JWT token.</param>
    /// <param name="sessionId">The session to renew, or null to create a new session.</param>
    /// <param name="previousTokenId">The current JWT token identifier required when renewing a session.</param>
    /// <returns>The issued session, or null if renewal is rejected.</returns>
    AuthSession? Issue(User user, string tokenId, string? sessionId, string? previousTokenId);

    /// <summary>
    /// Checks that the session is active, belongs to the user, references the supplied token,
    /// and still matches the user's password, email, and profile.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="tokenId">The JWT token identifier to validate.</param>
    /// <param name="userId">The identifier of the user associated with the token.</param>
    /// <returns>True if the session is valid; otherwise, false.</returns>
    bool Validate(string sessionId, string tokenId, int userId);

    /// <summary>
    /// Marks the specified session as revoked, preventing further validation or renewal.
    /// Does nothing if the session does not exist.
    /// </summary>
    /// <param name="sessionId">The identifier of the session to revoke.</param>
    void Revoke(string sessionId);
}
