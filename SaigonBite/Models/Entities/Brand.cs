using System;
using System.Collections.Generic;

namespace SaigonBite.Models.Entities;

public partial class Brand
{
    public int BrandId { get; set; }

    public string Name { get; set; } = null!;

    public bool IsActive { get; set; }

    public virtual ICollection<MenuItem> MenuItems { get; set; } = new List<MenuItem>();

    public virtual ICollection<Hub> Hubs { get; set; } = new List<Hub>();
}
