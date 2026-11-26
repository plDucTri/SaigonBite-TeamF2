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

    // UC1 (FR10, NFR5): reason for a rejection, cancellation or failed delivery.
    // Added by Database/04_alter_status_history_reason.sql. Re-scaffolding the model will generate it too.
    public string? Reason { get; set; }

    public virtual User ChangedByNavigation { get; set; } = null!;

    public virtual Order Order { get; set; } = null!;
}
