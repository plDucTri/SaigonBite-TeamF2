using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaigonBite.Helpers;
using SaigonBite.Repositories;
using SaigonBite.Services;
using SaigonBite.ViewModels;
using System.Security.Claims;

namespace SaigonBite.Controllers;

/// <summary>
/// Admin Portal: User Accounts. It is not part of AccountController because that class is
/// [AllowAnonymous], and AllowAnonymous would override the Admin check on these actions.
/// </summary>
[Authorize(Roles = Roles.Admin)]
[Route("Admin/Accounts")]
public class AdminAccountsController : Controller
{
    private const int PageSize = 10;

    private readonly IAccountService _accounts;

    public AdminAccountsController(IAccountService accounts)
    {
        _accounts = accounts;
    }

    // openAccountList(filter) in the sequence diagram
    [HttpGet("")]
    public IActionResult OpenAccountList(string? search, string? role, string? status, int page = 1)
    {
        var filter = new UserFilter
        {
            Search = search,
            Role = role,
            Status = status,
            Page = page,
            PageSize = PageSize
        };

        var vm = new AccountListViewModel
        {
            Search = search,
            Role = role,
            Status = status,
            CurrentUserId = CurrentUserId(),
            Result = _accounts.ListAccounts(filter)
        };

        return View(vm);
    }

    [HttpGet("{id:int}")]
    public IActionResult Details(int id)
    {
        var result = _accounts.GetAccount(id);
        if (!result.Ok)
            return AccountMissing();

        return View(result.Value);
    }

    [HttpGet("{id:int}/Edit")]
    public IActionResult Edit(int id)
    {
        var result = _accounts.GetAccount(id);
        if (!result.Ok)
            return AccountMissing();

        AccountInfo info = result.Value!;
        var vm = new EditAccountViewModel
        {
            FullName = info.FullName,
            Identifier = info.Identifier,
            Nationality = info.Nationality,
            Email = info.Email,
            HotelName = info.HotelName,
            RoomNo = info.RoomNo,
            HubId = info.HubId
        };
        FillDisplay(vm, info);

        return View(vm);
    }

    // updateAccount(userId, changes) in the sequence diagram
    [HttpPost("{id:int}/Edit"), ValidateAntiForgeryToken]
    public IActionResult UpdateAccount(int id, EditAccountViewModel vm)
    {
        var current = _accounts.GetAccount(id);
        if (!current.Ok)
            return AccountMissing();

        if (ModelState.IsValid && (vm.NewPassword ?? "") != (vm.ConfirmPassword ?? ""))
            ModelState.AddModelError(string.Empty, "Passwords do not match.");

        if (ModelState.IsValid)
        {
            var changes = new AccountChanges
            {
                FullName = vm.FullName ?? "",
                Identifier = vm.Identifier ?? "",
                NewPassword = vm.NewPassword ?? "",
                Nationality = vm.Nationality ?? "",
                Email = vm.Email ?? "",
                HotelName = vm.HotelName,
                RoomNo = vm.RoomNo,
                HubId = vm.HubId
            };

            var result = _accounts.UpdateAccount(id, changes);
            if (result.Ok)
            {
                TempData["Notice"] = "Account saved.";
                return RedirectToAction(nameof(Details), new { id });
            }

            AddErrors(result.Errors);
        }

        // Something was wrong: show the form again with what the admin typed.
        FillDisplay(vm, current.Value!);
        return View("Edit", vm);
    }

    // deactivateAccount(userId) in the sequence diagram: a soft delete, the row stays in the database
    [HttpPost("{id:int}/Deactivate"), ValidateAntiForgeryToken]
    public IActionResult DeactivateAccount(int id, string? returnUrl)
    {
        var result = _accounts.DeactivateAccount(id, CurrentUserId());

        if (result.Ok)
            TempData["Notice"] = "Account deactivated.";
        else
            TempData["Error"] = result.Errors[0];

        // Go back to the page the admin came from (only a page on this site).
        return BackTo(returnUrl);
    }

    [HttpPost("{id:int}/Reactivate"), ValidateAntiForgeryToken]
    public IActionResult ReactivateAccount(int id, string? returnUrl)
    {
        var result = _accounts.ReactivateAccount(id);

        if (result.Ok)
            TempData["Notice"] = "Account reactivated.";
        else
            TempData["Error"] = result.Errors[0];

        return BackTo(returnUrl);
    }

    [HttpGet("New")]
    public IActionResult Create()
    {
        return View(new CreateStaffViewModel { Hubs = _accounts.GetHubOptions() });
    }

    [HttpPost("New"), ValidateAntiForgeryToken]
    public IActionResult CreateStaffAccount(CreateStaffViewModel vm)
    {
        if (ModelState.IsValid && (vm.Password ?? "") != (vm.ConfirmPassword ?? ""))
            ModelState.AddModelError(string.Empty, "Passwords do not match.");

        if (ModelState.IsValid)
        {
            var request = new NewStaffAccount
            {
                FullName = vm.FullName ?? "",
                Role = vm.Role ?? "",
                Identifier = vm.Identifier ?? "",
                Password = vm.Password ?? "",
                HubId = vm.HubId
            };

            var result = _accounts.CreateStaffAccount(request);
            if (result.Ok)
            {
                TempData["Notice"] = "Account created";
                return RedirectToAction(nameof(Details), new { id = result.Value!.UserId });
            }

            AddErrors(result.Errors);
        }

        vm.Hubs = _accounts.GetHubOptions();
        return View("Create", vm);
    }

    // ---------- helpers ----------

    // The id of the signed-in admin (stored in the cookie at login).
    private int CurrentUserId() =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int id) ? id : 0;

    // A link to an account that no longer exists: go back to the list with a message.
    private IActionResult AccountMissing()
    {
        TempData["Error"] = "Account not found.";
        return RedirectToAction(nameof(OpenAccountList));
    }

    // The read-only parts of the Edit page (title, role, status, hub list).
    private void FillDisplay(EditAccountViewModel vm, AccountInfo info)
    {
        vm.Account = info;
        vm.Hubs = _accounts.GetHubOptions();
        vm.IsSelf = info.UserId == CurrentUserId();
    }

    private void AddErrors(IEnumerable<string> errors)
    {
        foreach (string error in errors)
            ModelState.AddModelError(string.Empty, error);
    }

    // Go back to the page the admin came from (only a page on this site), else to the list.
    private IActionResult BackTo(string? returnUrl)
    {
        if (Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl!);

        return RedirectToAction(nameof(OpenAccountList));
    }
}