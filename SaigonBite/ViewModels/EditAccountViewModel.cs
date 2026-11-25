using System.ComponentModel.DataAnnotations;
using SaigonBite.Services;

namespace SaigonBite.ViewModels;

/// <summary>
/// Posted fields are nullable on purpose: the real rules live in the service.
/// Account, Hubs and IsSelf are filled by the controller and never posted.
/// </summary>
public class EditAccountViewModel
{
    [StringLength(100, ErrorMessage = "Text is too long.")]
    public string? FullName { get; set; }

    [StringLength(30, ErrorMessage = "Text is too long.")]
    public string? Identifier { get; set; }

    [DataType(DataType.Password)]
    [StringLength(128, ErrorMessage = "Password is too long.")]
    public string? NewPassword { get; set; }

    [DataType(DataType.Password)]
    [StringLength(128, ErrorMessage = "Password is too long.")]
    public string? ConfirmPassword { get; set; }

    // Tourist only
    [StringLength(60, ErrorMessage = "Text is too long.")]
    public string? Nationality { get; set; }

    [StringLength(150, ErrorMessage = "Text is too long.")]
    public string? Email { get; set; }

    [StringLength(100, ErrorMessage = "Text is too long.")]
    public string? HotelName { get; set; }

    [StringLength(20, ErrorMessage = "Text is too long.")]
    public string? RoomNo { get; set; }

    // Kitchen staff only
    public int? HubId { get; set; }

    // ---- filled by the controller ----
    public AccountInfo? Account { get; set; }
    public IReadOnlyList<HubOption> Hubs { get; set; } = Array.Empty<HubOption>();
    public bool IsSelf { get; set; }
}