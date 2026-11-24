using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaigonBite.Helpers;
using SaigonBite.Services;
using SaigonBite.Validators;
using SaigonBite.ViewModels;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace SaigonBite.Controllers;

[AllowAnonymous]
public class AccountController : Controller
{
    private const string IdKey = "VerifyIdentifier";
    private const string TypeKey = "VerifyType";
    private const string DemoKey = "VerifyDemoCode";

    private static readonly Regex SixDigits = new(@"^[0-9]{6}\z", RegexOptions.Compiled);

    private readonly IAccountService _accounts;

    public AccountController(IAccountService accounts)
    {
        _accounts = accounts;
    }

    // ---------- Register Local ----------

    [HttpGet]
    public IActionResult RegisterLocal() => View(new RegisterLocalViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult RegisterLocal(RegisterLocalViewModel vm)
    {
        if (!ModelState.IsValid)
            return View(vm);

        if ((vm.Password ?? "") != (vm.ConfirmPassword ?? ""))
        {
            ModelState.AddModelError(string.Empty, "Passwords do not match.");
            return View(vm);
        }

        var request = new AccountRequest
        {
            Type = Roles.Local,
            Identifier = vm.PhoneNumber ?? "",
            FullName = vm.FullName ?? "",
            Password = vm.Password ?? ""
        };

        return StartVerification(request, vm);
    }

    // ---------- Register Tourist ----------

    [HttpGet]
    public IActionResult RegisterTourist() => View(new RegisterTouristViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult RegisterTourist(RegisterTouristViewModel vm)
    {
        if (!ModelState.IsValid)
            return View(vm);

        if ((vm.Password ?? "") != (vm.ConfirmPassword ?? ""))
        {
            ModelState.AddModelError(string.Empty, "Passwords do not match.");
            return View(vm);
        }

        var request = new AccountRequest
        {
            Type = Roles.Tourist,
            Identifier = vm.PassportNo ?? "",
            FullName = vm.FullName ?? "",
            Password = vm.Password ?? "",
            Nationality = vm.Nationality ?? "",
            Email = vm.Email ?? "",
            HotelName = vm.HotelName,
            RoomNo = vm.RoomNo
        };

        return StartVerification(request, vm);
    }

    // ---------- Verify ----------

    [HttpGet]
    public IActionResult Verify()
    {
        if (TempData.Peek(IdKey) is not string)
            return RedirectToAction(nameof(RegisterLocal)); // nothing to verify (page opened directly)

        if (TempData["VerifyError"] is string error)
            ModelState.AddModelError(string.Empty, error);

        return View(BuildVerifyModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Verify(VerifyViewModel vm)
    {
        vm.DemoCode = TempData.Peek(DemoKey) as string; // never trust the posted value
        string code = (vm.Code ?? "").Trim();
        ModelState.Remove(nameof(vm.Code));
        vm.Code = string.Empty; // clear the box so the person can retry

        if (!SixDigits.IsMatch(code))
        {
            ModelState.AddModelError(string.Empty, "Enter the six-digit verification code.");
            return View(vm);
        }

        var result = _accounts.ConfirmRegistration(vm.Identifier ?? "", code);
        if (!result.Ok)
        {
            AddErrors(result.Errors);
            return View(vm);
        }

        TempData.Remove(IdKey);
        TempData.Remove(TypeKey);
        TempData.Remove(DemoKey);
        return RedirectToAction(nameof(Registered));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Resend(string? identifier, string? type)
    {
        var result = _accounts.ResendCode(identifier ?? "");
        if (result.Ok)
        {
            Remember(result.Value!.Identifier, type ?? "", result.Value.DemoCode);
            TempData["Notice"] = "A new code has been sent.";
        }
        else
        {
            TempData["VerifyError"] = result.Errors[0];
        }

        return RedirectToAction(nameof(Verify));
    }

    [HttpGet]
    public IActionResult Registered() => View();

    // ---------- Login / Logout ----------

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel vm)
    {
        if (!ModelState.IsValid)
            return View(vm);

        var result = _accounts.Login(vm.Identifier ?? "", vm.Password ?? "");
        if (!result.Ok)
        {
            AddErrors(result.Errors);
            return View(vm);
        }

        // The cookie keeps the user id, name and role.
        SignedInUser user = result.Value!;
        var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
        new(ClaimTypes.Name, user.FullName),
        new(ClaimTypes.Role, user.Role)
    };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

        // Only go back to a page on this site.
        if (Url.IsLocalUrl(vm.ReturnUrl))
            return LocalRedirect(vm.ReturnUrl!);

        return RedirectToAction("Index", "Home");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult AccessDenied() => View();

    // ---------- helpers ----------

    private IActionResult StartVerification(AccountRequest request, object viewModel)
    {
        var result = _accounts.RegisterAccount(request);
        if (!result.Ok)
        {
            AddErrors(result.Errors);
            return View(viewModel); // same view as the current action
        }

        Remember(result.Value!.Identifier, request.Type, result.Value.DemoCode);
        return RedirectToAction(nameof(Verify));
    }

    private void Remember(string identifier, string type, string? demoCode)
    {
        TempData[IdKey] = identifier;
        TempData[TypeKey] = type;
        TempData[DemoKey] = demoCode ?? string.Empty;
    }

    private VerifyViewModel BuildVerifyModel() => new()
    {
        Identifier = TempData.Peek(IdKey) as string ?? "",
        Type = TempData.Peek(TypeKey) as string ?? "",
        DemoCode = TempData.Peek(DemoKey) as string
    };

    private void AddErrors(IEnumerable<string> errors)
    {
        foreach (string error in errors)
            ModelState.AddModelError(string.Empty, error);
    }
}
