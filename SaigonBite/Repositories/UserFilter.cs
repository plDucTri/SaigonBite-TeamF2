namespace SaigonBite.Repositories;

/// <summary>What the admin typed in the search box and filters, plus the page wanted.</summary>
public class UserFilter
{
    public string? Search { get; set; }
    public string? Role { get; set; }    // one of the Roles constants, empty = all roles
    public string? Status { get; set; }  // AccountStatus.Active or AccountStatus.Inactive, empty = all
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}