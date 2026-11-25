using System.ComponentModel.DataAnnotations;
using SaigonBite.Helpers;
using SaigonBite.Services;

namespace SaigonBite.ViewModels;

/// <summary>Posted fields are nullable on purpose: the real rules live in the service.</summary>
public class CreateStaffViewModel
{
    [StringLength(100, ErrorMessage = "Text is too long.")]
    public string? FullName { get; set; }

    public string? Role { get; set; } = Roles.Kitchen;

    [StringLength(30, ErrorMessage = "Text is too long.")]
    public string? Identifier { get; set; }

    public int? HubId { get; set; }

    [DataType(DataType.Password)]
    [StringLength(128, ErrorMessage = "Password is too long.")]
    public string? Password { get; set; }

    [DataType(DataType.Password)]
    [StringLength(128, ErrorMessage = "Password is too long.")]
    public string? ConfirmPassword { get; set; }

    // Filled by the controller, never posted.
    public IReadOnlyList<HubOption> Hubs { get; set; } = Array.Empty<HubOption>();
}