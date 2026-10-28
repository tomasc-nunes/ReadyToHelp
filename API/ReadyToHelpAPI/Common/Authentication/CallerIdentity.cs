namespace readytohelpapi.Common.Authentication;

using System.Globalization;
using System.Security.Claims;

public static class CallerIdentity
{
    public static bool TryGetUserId(this ClaimsPrincipal principal, out int userId)
    {
        var subject = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("sub");
        return int.TryParse(subject, NumberStyles.None, CultureInfo.InvariantCulture, out userId)
            && userId > 0;
    }
}
