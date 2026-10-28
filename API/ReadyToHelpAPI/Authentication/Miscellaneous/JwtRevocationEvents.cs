namespace readytohelpapi.Authentication.Miscellaneous;

using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using readytohelpapi.Authentication.Service;
using Microsoft.Extensions.Logging;
using readytohelpapi.Common.Authentication;

/// <summary>
///   Class responsible for handling JWT revocation events.
/// </summary>
public static class JwtRevocationEvents
{
    /// <summary>
    ///   Creates JwtBearerEvents with token revocation checking.
    /// </summary>
    public static JwtBearerEvents Create() =>
        new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                return Task.CompletedTask;
            },

            OnTokenValidated = async context =>
            {
                try
                {
                    var store = context.HttpContext.RequestServices
                        .GetRequiredService<ITokenRevocationStore>();

                    var jti =
                        context.Principal?.FindFirst(JwtRegisteredClaimNames.Jti)?.Value
                        ?? (context.SecurityToken as JwtSecurityToken)?.Id;
                    if (string.IsNullOrWhiteSpace(jti))
                    {
                        context.Fail("Token identifier is required.");
                        return;
                    }

                    if (await store.IsRevokedAsync(jti, context.HttpContext.RequestAborted))
                    {
                        context.Fail("Token has been revoked.");
                    }
                    var sessionId = context.Principal?.FindFirst("sid")?.Value;
                    if (sessionId == null || !context.Principal!.TryGetUserId(out var userId)
                        || !context.HttpContext.RequestServices.GetRequiredService<IAuthSessionStore>()
                            .Validate(sessionId, jti, userId))
                        context.Fail("Session is no longer valid.");
                }
                catch (Exception ex)
                {
                    var loggerFactory =
                        context.HttpContext.RequestServices.GetService(typeof(ILoggerFactory))
                        as ILoggerFactory;
                    loggerFactory
                        ?.CreateLogger("JwtBearer")
                        .LogWarning(ex, "Unable to verify token revocation; rejecting authentication.");
                    context.Fail("Unable to verify token revocation.");
                }
            },
        };
}
