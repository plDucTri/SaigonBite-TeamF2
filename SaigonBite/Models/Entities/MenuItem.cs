using System;
using System.Collections.Generic;

namespace SaigonBite.Models.Entities;

public partial class MenuItem
{
    public int ItemId { get; set; }

    public int BrandId { get; set; }

    public string Name { get; set; } = null!;

    public string Category { get; set; } = null!;

    public decimal Price { get; set; }

    public bool IsAvailable { get; set; }

    public bool IsActive { get; set; }

    public virtual Brand Brand { get; set; } = null!;

    public virtual ICollection<OrderLine> OrderLines { get; set; } = new List<OrderLine>();
}
