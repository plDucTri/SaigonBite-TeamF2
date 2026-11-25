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

    // ---------- helpers ----------

    // The id of the signed-in admin (stored in the cookie at login).
    private int CurrentUserId() =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int id) ? id : 0;
}