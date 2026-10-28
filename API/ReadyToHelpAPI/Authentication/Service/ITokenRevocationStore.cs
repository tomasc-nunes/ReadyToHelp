namespace readytohelpapi.Authentication.Service;

/// <summary>
/// Defines the contract for storing token revocations and checking whether tokens are revoked.
/// </summary>
public interface ITokenRevocationStore
{
    /// <summary>
    /// Checks whether the token has a revocation record that has not expired.
    /// </summary>
    /// <param name="id">The JWT token identifier.</param>
    /// <returns>True if an unexpired revocation exists; otherwise, false.</returns>
    bool IsRevoked(string id);

    /// <summary>
    /// Asynchronously checks whether the token has a revocation record that has not expired.
    /// </summary>
    /// <param name="id">The JWT token identifier.</param>
    /// <param name="cancellationToken">The cancellation token for the database query.</param>
    /// <returns>True if an unexpired revocation exists; otherwise, false.</returns>
    Task<bool> IsRevokedAsync(string id, CancellationToken cancellationToken);

    /// <summary>
    /// Stores a token revocation until its expiration and removes expired revocation records.
    /// Repeated calls retain the latest expiration recorded for the token.
    /// </summary>
    /// <param name="id">The identifier of the JWT token to revoke.</param>
    /// <param name="expiresAt">The UTC expiration time of the token.</param>
    void Revoke(string id, DateTime expiresAt);
}
