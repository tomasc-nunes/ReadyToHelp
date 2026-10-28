namespace readytohelpapi.Authentication.Service;

using Microsoft.EntityFrameworkCore;
using readytohelpapi.Common.Data;

public class TokenRevocationStore(AppDbContext context) : ITokenRevocationStore
{
    /// <inheritdoc />
    public bool IsRevoked(string id) => context.RevokedTokens
        .Any(token => token.Id == id && token.ExpiresAt > DateTime.UtcNow);

    /// <inheritdoc />
    public Task<bool> IsRevokedAsync(string id, CancellationToken cancellationToken) =>
        context.RevokedTokens.AnyAsync(
            token => token.Id == id && token.ExpiresAt > DateTime.UtcNow, cancellationToken);

    /// <inheritdoc />
    public void Revoke(string id, DateTime expiresAt)
    {
        // Idempotent even when simultaneous logout requests reach different API instances.
        context.Database.ExecuteSqlInterpolated($"""
            INSERT INTO revoked_tokens ("Id", "ExpiresAt") VALUES ({id}, {expiresAt})
            ON CONFLICT ("Id") DO UPDATE
            SET "ExpiresAt" = GREATEST(revoked_tokens."ExpiresAt", EXCLUDED."ExpiresAt")
            """);
        context.RevokedTokens.Where(token => token.ExpiresAt <= DateTime.UtcNow).ExecuteDelete();
    }
}
