using System;
using System.Collections.Generic;

namespace SaigonBite.Models.Entities;

public partial class TouristProfile
{
    public int UserId { get; set; }

    public string PassportNo { get; set; } = null!;

    public string Nationality { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string? HotelName { get; set; }

    public string? RoomNo { get; set; }

    public virtual User User { get; set; } = null!;
}
