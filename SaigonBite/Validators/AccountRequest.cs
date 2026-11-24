namespace SaigonBite.Validators;

/// <summary>
/// Data of one registration (or edit). Type is "Local" or "Tourist" (see Roles).
/// Identifier is the phone number for Local and the passport number for Tourist (A-04).
/// </summary>
public class AccountRequest
{
    public string Type { get; set; } = string.Empty;
    public string Identifier { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    // Tourist only
    public string Nationality { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? HotelName { get; set; }
    public string? RoomNo { get; set; }
}
