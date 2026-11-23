using System;
using System.Collections.Generic;

namespace SaigonBite.Models.Entities;

public partial class Payment
{
    public int PaymentId { get; set; }

    public int OrderId { get; set; }

    public string Method { get; set; } = null!;

    public decimal Amount { get; set; }

    public string Status { get; set; } = null!;

    public bool Refunded { get; set; }

    public virtual Order Order { get; set; } = null!;
}
