using System;
using System.Collections.Generic;

namespace SaigonBite.Models.Entities;

public partial class User
{
    public int UserId { get; set; }

    public string Identifier { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public string Role { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public bool IsActive { get; set; }

    public int? HubId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Hub? Hub { get; set; }

    public virtual LocalProfile? LocalProfile { get; set; }

    public virtual ICollection<Order> OrderCustomers { get; set; } = new List<Order>();

    public virtual ICollection<Order> OrderShippers { get; set; } = new List<Order>();

    public virtual ICollection<OrderStatusHistory> OrderStatusHistories { get; set; } = new List<OrderStatusHistory>();

    public virtual TouristProfile? TouristProfile { get; set; }
}
