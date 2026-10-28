namespace readytohelpapi.Authentication.Service;

using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using readytohelpapi.Authentication.Models;
using readytohelpapi.Common.Data;
using readytohelpapi.User.Models;

public class AuthSessionStore(AppDbContext context) : IAuthSessionStore
{
    /// <summary>
    ///  Generates a unique stamp for the user based on their password, email, and profile.
    /// </summary>
    /// <param name="user"> The user for which to generate the stamp. </param>
    /// <returns> The generated stamp. </returns>
    private static string Stamp(User user) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(new { user.Password, user.Email, user.Profile }))));

    /// <inheritdoc />
    public AuthSession? Issue(User user, string tokenId, string? sessionId, string? previousTokenId)
    {
        using var transaction = context.Database.BeginTransaction();
        AuthSession session;
        if (sessionId == null)
        {
            session = new AuthSession { UserId = user.Id, AccountStamp = Stamp(user), ExpiresAt = DateTime.UtcNow.AddDays(7) };
            context.AuthSessions.Add(session);
        }
        else
        {
            session = context.AuthSessions.FromSqlInterpolated(
                $"SELECT * FROM auth_sessions WHERE \"Id\" = {sessionId} FOR UPDATE").SingleOrDefault()!;
            if (session == null) return null;
            var currentUser = context.Users.AsNoTracking().SingleOrDefault(u => u.Id == session.UserId);
            if (session.Revoked || session.ExpiresAt <= DateTime.UtcNow || session.TokenId != previousTokenId
                || currentUser == null || session.UserId != user.Id || session.AccountStamp != Stamp(currentUser)
                || session.AccountStamp != Stamp(user))
            {
                session.Revoked = true;
                context.SaveChanges();
                transaction.Commit();
                return null;
            }
        }
        session.TokenId = tokenId;
        context.SaveChanges();
        transaction.Commit();
        return session;
    }

    /// <inheritdoc />
    public bool Validate(string sessionId, string tokenId, int userId)
    {
        var session = context.AuthSessions.AsNoTracking().SingleOrDefault(s => s.Id == sessionId);
        if (session == null || session.Revoked || session.ExpiresAt <= DateTime.UtcNow
            || session.UserId != userId || session.TokenId != tokenId) return false;
        var user = context.Users.AsNoTracking().SingleOrDefault(u => u.Id == userId);
        return user != null && session.AccountStamp == Stamp(user);
    }

    /// <inheritdoc />
    public void Revoke(string sessionId) => context.AuthSessions.Where(s => s.Id == sessionId)
        .ExecuteUpdate(setters => setters.SetProperty(s => s.Revoked, true));
}
