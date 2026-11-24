using System.ComponentModel.DataAnnotations;

namespace SaigonBite.ViewModels;

public class VerifyViewModel
{
    public string? Identifier { get; set; }

    /// <summary>"Local" or "Tourist". Used only to show which kind of code was sent.</summary>
    public string? Type { get; set; }

    [StringLength(10, ErrorMessage = "Text is too long.")]
    public string? Code { get; set; }

    /// <summary>The simulated code, shown on screen because nothing is really sent. Never posted back.</summary>
    public string? DemoCode { get; set; }
}
