using System;
using System.Collections.Generic;

namespace SaigonBite.Models.Entities;

public partial class LocalProfile
{
    public int UserId { get; set; }

    public string PhoneNumber { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
