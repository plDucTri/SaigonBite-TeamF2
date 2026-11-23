using System;
using System.Collections.Generic;

namespace SaigonBite.Models.Entities;

public partial class Hub
{
    public int HubId { get; set; }

    public string Name { get; set; } = null!;

    public string Address { get; set; } = null!;

    public int KitchenCapacity { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    public virtual ICollection<User> Users { get; set; } = new List<User>();

    public virtual ICollection<Brand> Brands { get; set; } = new List<Brand>();
}
