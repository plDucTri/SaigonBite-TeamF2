using System.Security.Claims;

namespace SaigonBite.Helpers;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// The logged-in Users.UserId, read from the NameIdentifier claim that AccountController.Login puts in the cookie.
    /// Returns 0 when nobody is logged in; the services then refuse the call.
    /// </summary>
    public static int GetUserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(value, out var id) ? id : 0;
    }
}
