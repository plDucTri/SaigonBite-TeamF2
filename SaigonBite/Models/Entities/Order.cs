using System;
using System.Collections.Generic;

namespace SaigonBite.Models.Entities;

public partial class Order
{
    public int OrderId { get; set; }

    public int CustomerId { get; set; }

    public int HubId { get; set; }

    public int? ShipperId { get; set; }

    public string? VoucherCode { get; set; }

    public string Status { get; set; } = null!;

    public decimal DistanceKm { get; set; }

    public string DeliveryAddress { get; set; } = null!;

    public decimal SubtotalS { get; set; }

    public decimal QuietDiscountQ { get; set; }

    public decimal VoucherDiscountV { get; set; }

    public decimal DeliveryFeeD { get; set; }

    public decimal RushSurchargeR { get; set; }

    public decimal RainSurchargeR2 { get; set; }

    public decimal Total { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User Customer { get; set; } = null!;

    public virtual Hub Hub { get; set; } = null!;

    public virtual ICollection<OrderLine> OrderLines { get; set; } = new List<OrderLine>();

    public virtual ICollection<OrderStatusHistory> OrderStatusHistories { get; set; } = new List<OrderStatusHistory>();

    public virtual Payment? Payment { get; set; }

    public virtual PriceSnapshot? PriceSnapshot { get; set; }

    public virtual User? Shipper { get; set; }

    public virtual Voucher? VoucherCodeNavigation { get; set; }
}
