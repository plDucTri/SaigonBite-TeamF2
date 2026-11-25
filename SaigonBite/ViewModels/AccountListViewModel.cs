using SaigonBite.Helpers;
using SaigonBite.Services;

namespace SaigonBite.ViewModels;

public class AccountListViewModel
{
    // What the admin typed, so the boxes keep their values.
    public string? Search { get; set; }
    public string? Role { get; set; }
    public string? Status { get; set; }

    /// <summary>The signed-in admin. Used to hide Deactivate on their own row.</summary>
    public int CurrentUserId { get; set; }

    public PagedResult<AccountInfo> Result { get; set; } = new(Array.Empty<AccountInfo>(), 0, 1, 10);
}