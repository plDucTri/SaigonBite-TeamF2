using System;
using System.Collections.Generic;

namespace SaigonBite.Models.Entities;

public partial class OrderStatusHistory
{
    public int HistoryId { get; set; }

    public int OrderId { get; set; }

    public string? FromStatus { get; set; }

    public string ToStatus { get; set; } = null!;

    public DateTime ChangedAt { get; set; }

    public int ChangedBy { get; set; }

    public virtual User ChangedByNavigation { get; set; } = null!;

    public virtual Order Order { get; set; } = null!;
}
