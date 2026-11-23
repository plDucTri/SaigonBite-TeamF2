using System;
using System.Collections.Generic;

namespace SaigonBite.Models.Entities;

public partial class PriceSnapshot
{
    public int OrderId { get; set; }

    public DateTime PlacedAt { get; set; }

    public decimal HubLoadPct { get; set; }

    public bool HeavyRain { get; set; }

    public virtual Order Order { get; set; } = null!;
}
