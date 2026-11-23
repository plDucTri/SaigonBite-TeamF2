using System;
using System.Collections.Generic;

namespace SaigonBite.Models.Entities;

public partial class OrderLine
{
    public int OrderId { get; set; }

    public int ItemId { get; set; }

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public virtual MenuItem Item { get; set; } = null!;

    public virtual Order Order { get; set; } = null!;
}
