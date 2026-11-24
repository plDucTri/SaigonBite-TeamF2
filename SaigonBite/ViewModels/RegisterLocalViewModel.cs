using System.ComponentModel.DataAnnotations;

namespace SaigonBite.ViewModels;

/// <summary>Properties are nullable on purpose: the real rules (and their messages) live in AccountValidator.</summary>
public class RegisterLocalViewModel
{
    [StringLength(100, ErrorMessage = "Text is too long.")]
    public string? FullName { get; set; }

    [StringLength(20, ErrorMessage = "Text is too long.")]
    public string? PhoneNumber { get; set; }

    [DataType(DataType.Password)]
    [StringLength(128, ErrorMessage = "Password is too long.")]
    public string? Password { get; set; }

    [DataType(DataType.Password)]
    [StringLength(128, ErrorMessage = "Password is too long.")]
    public string? ConfirmPassword { get; set; }
}
