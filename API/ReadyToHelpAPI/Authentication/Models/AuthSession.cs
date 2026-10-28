namespace readytohelpapi.Authentication.Models;

/// <summary>
/// Represents a user's authentication session and its current token.
/// </summary>
public sealed class AuthSession
{
    /// <summary>
    /// Gets or sets the unique session identifier.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Gets or sets the identifier of the user who owns the session.
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the JWT currently associated with the session.
    /// </summary>
    public string TokenId { get; set; } = "";

    /// <summary>
    /// Gets or sets the hash used to detect changes to the user's password, email, or profile.
    /// </summary>
    public string AccountStamp { get; set; } = "";

    /// <summary>
    /// Gets or sets the fixed UTC expiration time of the session.
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets whether the session has been revoked.
    /// </summary>
    public bool Revoked { get; set; }
}
