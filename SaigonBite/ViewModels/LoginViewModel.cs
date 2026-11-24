using System.ComponentModel.DataAnnotations;

namespace SaigonBite.ViewModels;

/// <summary>Only the length is checked here. The service decides if the login is right.</summary>
public class LoginViewModel
{
    [StringLength(30, ErrorMessage = "Text is too long.")]
    public string? Identifier { get; set; }

    [DataType(DataType.Password)]
    [StringLength(128, ErrorMessage = "Password is too long.")]
    public string? Password { get; set; }

    /// <summary>Page the person wanted before being sent to the login page.</summary>
    public string? ReturnUrl { get; set; }
}
