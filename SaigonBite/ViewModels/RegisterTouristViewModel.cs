using System.ComponentModel.DataAnnotations;

namespace SaigonBite.ViewModels;

public class RegisterTouristViewModel
{
    [StringLength(100, ErrorMessage = "Text is too long.")]
    public string? FullName { get; set; }

    [StringLength(20, ErrorMessage = "Text is too long.")]
    public string? PassportNo { get; set; }

    [StringLength(60, ErrorMessage = "Text is too long.")]
    public string? Nationality { get; set; }

    [StringLength(100, ErrorMessage = "Text is too long.")]
    public string? Email { get; set; }

    /// <summary>"Hotel / Delivery Address" in the design.</summary>
    [StringLength(150, ErrorMessage = "Text is too long.")]
    public string? HotelName { get; set; }

    [StringLength(20, ErrorMessage = "Text is too long.")]
    public string? RoomNo { get; set; }

    [DataType(DataType.Password)]
    [StringLength(128, ErrorMessage = "Password is too long.")]
    public string? Password { get; set; }

    [DataType(DataType.Password)]
    [StringLength(128, ErrorMessage = "Password is too long.")]
    public string? ConfirmPassword { get; set; }
}
