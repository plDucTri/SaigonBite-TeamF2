namespace SaigonBite.Helpers;

/// <summary>Names shown on screen for the role values (the values themselves are in Roles).</summary>
public static class RoleLabels
{
    public static readonly string[] All = { Roles.Local, Roles.Tourist, Roles.Kitchen, Roles.Shipper, Roles.Admin };

    public static string For(string role) => role switch
    {
        Roles.Local => "Local Customer",
        Roles.Tourist => "Foreign Tourist",
        Roles.Kitchen => "Kitchen Staff",
        Roles.Shipper => "Shipper",
        Roles.Admin => "Admin",
        _ => role
    };

    /// <summary>Label of the identifier field: phone for Local, passport for Tourist, staff ID for the others.</summary>
    public static string IdentifierLabel(string role) => role switch
    {
        Roles.Local => "Phone Number",
        Roles.Tourist => "Passport Number",
        _ => "Staff Identifier"
    };
}