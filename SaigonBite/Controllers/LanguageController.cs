using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace SaigonBite.Controllers;

public class LanguageController : Controller
{
    private static readonly string[] SupportedCultures = { "en", "vi" };

    [HttpGet]
    public IActionResult Set(string culture, string? returnUrl)
    {
        if (!SupportedCultures.Contains(culture))
            culture = "en";

        Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true,
                SameSite = SameSiteMode.Lax
            });

        // Only redirect inside this site (no open redirect).
        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/");
    }
}
