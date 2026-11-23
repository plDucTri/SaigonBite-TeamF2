using System;
using System.Collections.Generic;

namespace SaigonBite.Models.Entities;

public partial class Voucher
{
    public string Code { get; set; } = null!;

    public string Type { get; set; } = null!;

    public decimal Value { get; set; }

    public decimal? MaxDiscount { get; set; }

    public decimal MinSubtotal { get; set; }

    public DateOnly ValidFrom { get; set; }

    public DateOnly ValidTo { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
}
