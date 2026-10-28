namespace readytohelpapi.Authentication.Models;

/// <summary>
/// Represents a JWT revocation retained until the token expires.
/// </summary>
public sealed class RevokedToken
{
    /// <summary>
    /// Gets or sets the identifier of the revoked JWT.
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Gets or sets the UTC expiration time of the revoked token.
    /// </summary>
    public DateTime ExpiresAt { get; set; }
}
