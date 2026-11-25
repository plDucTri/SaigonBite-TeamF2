namespace SaigonBite.Services;

/// <summary>An account as the admin pages need it. It has no password hash, so a hash can never be shown.</summary>
public record AccountInfo(
    int UserId,
    string Identifier,
    string FullName,
    string Role,
    bool IsActive,
    DateTime CreatedAt,
    string? Email,        // tourist only
    string? Nationality,  // tourist only
    string? HotelName,    // tourist only
    string? RoomNo,       // tourist only
    int? HubId,           // kitchen staff only
    string? HubName);

/// <summary>One entry of the Assigned Hub drop-down.</summary>
public record HubOption(int HubId, string Name);

/// <summary>What the admin can change on the Edit page. An empty NewPassword means "keep the old one".</summary>
public class AccountChanges
{
    public string FullName { get; set; } = string.Empty;
    public string Identifier { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;

    // Tourist only
    public string Nationality { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? HotelName { get; set; }
    public string? RoomNo { get; set; }

    // Kitchen staff only
    public int? HubId { get; set; }
}

/// <summary>A new Kitchen, Shipper or Admin account made by an admin (customers register themselves).</summary>
public class NewStaffAccount
{
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Identifier { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public int? HubId { get; set; } // kitchen staff only
}