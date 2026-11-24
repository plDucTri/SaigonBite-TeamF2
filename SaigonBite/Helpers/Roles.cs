namespace SaigonBite.Helpers;

/// <summary>
/// Role names stored in Users.Role. Use these constants in [Authorize(Roles = ...)]
/// so every teammate spells them the same way.
/// </summary>
public static class Roles
{
    public const string Local = "Local";
    public const string Tourist = "Tourist";
    public const string Kitchen = "Kitchen";
    public const string Shipper = "Shipper";
    public const string Admin = "Admin";

    /// <summary>For [Authorize(Roles = Roles.Customers)].</summary>
    public const string Customers = Local + "," + Tourist;
}
