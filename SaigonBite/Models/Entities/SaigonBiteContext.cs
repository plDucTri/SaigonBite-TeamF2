using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace SaigonBite.Models.Entities;

public partial class SaigonBiteContext : DbContext
{
    public SaigonBiteContext(DbContextOptions<SaigonBiteContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Brand> Brands { get; set; }

    public virtual DbSet<Hub> Hubs { get; set; }

    public virtual DbSet<LocalProfile> LocalProfiles { get; set; }

    public virtual DbSet<MenuItem> MenuItems { get; set; }

    public virtual DbSet<Order> Orders { get; set; }

    public virtual DbSet<OrderLine> OrderLines { get; set; }

    public virtual DbSet<OrderStatusHistory> OrderStatusHistories { get; set; }

    public virtual DbSet<Payment> Payments { get; set; }

    public virtual DbSet<PriceSnapshot> PriceSnapshots { get; set; }

    public virtual DbSet<TouristProfile> TouristProfiles { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<Voucher> Vouchers { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Brand>(entity =>
        {
            entity.HasKey(e => e.BrandId).HasName("PK__Brands__DAD4F05E848DC073");

            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name).HasMaxLength(100);
        });

        modelBuilder.Entity<Hub>(entity =>
        {
            entity.HasKey(e => e.HubId).HasName("PK__Hubs__9F4FFEEF625BA33E");

            entity.Property(e => e.Address).HasMaxLength(200);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name).HasMaxLength(100);

            entity.HasMany(d => d.Brands).WithMany(p => p.Hubs)
                .UsingEntity<Dictionary<string, object>>(
                    "HubBrand",
                    r => r.HasOne<Brand>().WithMany()
                        .HasForeignKey("BrandId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK__HubBrands__Brand__60A75C0F"),
                    l => l.HasOne<Hub>().WithMany()
                        .HasForeignKey("HubId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK__HubBrands__HubId__5FB337D6"),
                    j =>
                    {
                        j.HasKey("HubId", "BrandId").HasName("PK__HubBrand__62E2B1EA14CC3AF9");
                        j.ToTable("HubBrands");
                    });
        });

        modelBuilder.Entity<LocalProfile>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__LocalPro__1788CC4C9322B181");

            entity.HasIndex(e => e.PhoneNumber, "UQ__LocalPro__85FB4E38221E1138").IsUnique();

            entity.Property(e => e.UserId).ValueGeneratedNever();
            entity.Property(e => e.PhoneNumber).HasMaxLength(10);

            entity.HasOne(d => d.User).WithOne(p => p.LocalProfile)
                .HasForeignKey<LocalProfile>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__LocalProf__UserI__5629CD9C");
        });

        modelBuilder.Entity<MenuItem>(entity =>
        {
            entity.HasKey(e => e.ItemId).HasName("PK__MenuItem__727E838B10775D50");

            entity.Property(e => e.Category).HasMaxLength(50);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IsAvailable).HasDefaultValue(true);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.Price).HasColumnType("decimal(12, 2)");

            entity.HasOne(d => d.Brand).WithMany(p => p.MenuItems)
                .HasForeignKey(d => d.BrandId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__MenuItems__Brand__6383C8BA");
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.OrderId).HasName("PK__Orders__C3905BCF947DF305");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.DeliveryAddress).HasMaxLength(300);
            entity.Property(e => e.DeliveryFeeD).HasColumnType("decimal(12, 2)");
            entity.Property(e => e.DistanceKm).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.QuietDiscountQ).HasColumnType("decimal(12, 2)");
            entity.Property(e => e.RainSurchargeR2).HasColumnType("decimal(12, 2)");
            entity.Property(e => e.RushSurchargeR).HasColumnType("decimal(12, 2)");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("Placed");
            entity.Property(e => e.SubtotalS).HasColumnType("decimal(12, 2)");
            entity.Property(e => e.Total).HasColumnType("decimal(12, 0)");
            entity.Property(e => e.VoucherCode).HasMaxLength(30);
            entity.Property(e => e.VoucherDiscountV).HasColumnType("decimal(12, 2)");

            entity.HasOne(d => d.Customer).WithMany(p => p.OrderCustomers)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Orders__Customer__6FE99F9F");

            entity.HasOne(d => d.Hub).WithMany(p => p.Orders)
                .HasForeignKey(d => d.HubId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Orders__HubId__70DDC3D8");

            entity.HasOne(d => d.Shipper).WithMany(p => p.OrderShippers)
                .HasForeignKey(d => d.ShipperId)
                .HasConstraintName("FK__Orders__ShipperI__71D1E811");

            entity.HasOne(d => d.VoucherCodeNavigation).WithMany(p => p.Orders)
                .HasForeignKey(d => d.VoucherCode)
                .HasConstraintName("FK__Orders__VoucherC__72C60C4A");
        });

        modelBuilder.Entity<OrderLine>(entity =>
        {
            entity.HasKey(e => new { e.OrderId, e.ItemId }).HasName("PK__OrderLin__64B7B3F7790DB525");

            entity.Property(e => e.UnitPrice).HasColumnType("decimal(12, 2)");

            entity.HasOne(d => d.Item).WithMany(p => p.OrderLines)
                .HasForeignKey(d => d.ItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__OrderLine__ItemI__7F2BE32F");

            entity.HasOne(d => d.Order).WithMany(p => p.OrderLines)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__OrderLine__Order__7E37BEF6");
        });

        modelBuilder.Entity<OrderStatusHistory>(entity =>
        {
            entity.HasKey(e => e.HistoryId).HasName("PK__OrderSta__4D7B4ABDF3E0486D");

            entity.ToTable("OrderStatusHistory");

            entity.Property(e => e.ChangedAt).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.FromStatus).HasMaxLength(20);
            entity.Property(e => e.ToStatus).HasMaxLength(20);

            entity.HasOne(d => d.ChangedByNavigation).WithMany(p => p.OrderStatusHistories)
                .HasForeignKey(d => d.ChangedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__OrderStat__Chang__0D7A0286");

            entity.HasOne(d => d.Order).WithMany(p => p.OrderStatusHistories)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__OrderStat__Order__0B91BA14");
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.PaymentId).HasName("PK__Payments__9B556A38A3EEF125");

            entity.HasIndex(e => e.OrderId, "UQ__Payments__C3905BCEB11DCE5D").IsUnique();

            entity.Property(e => e.Amount).HasColumnType("decimal(12, 0)");
            entity.Property(e => e.Method).HasMaxLength(20);
            entity.Property(e => e.Status).HasMaxLength(20);

            entity.HasOne(d => d.Order).WithOne(p => p.Payment)
                .HasForeignKey<Payment>(d => d.OrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Payments__OrderI__06CD04F7");
        });

        modelBuilder.Entity<PriceSnapshot>(entity =>
        {
            entity.HasKey(e => e.OrderId).HasName("PK__PriceSna__C3905BCFA505B6AD");

            entity.Property(e => e.OrderId).ValueGeneratedNever();
            entity.Property(e => e.HubLoadPct).HasColumnType("decimal(5, 2)");

            entity.HasOne(d => d.Order).WithOne(p => p.PriceSnapshot)
                .HasForeignKey<PriceSnapshot>(d => d.OrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__PriceSnap__Order__02FC7413");
        });

        modelBuilder.Entity<TouristProfile>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__TouristP__1788CC4C71732883");

            entity.HasIndex(e => e.PassportNo, "UQ__TouristP__185526FECFD05E51").IsUnique();

            entity.Property(e => e.UserId).ValueGeneratedNever();
            entity.Property(e => e.Email).HasMaxLength(150);
            entity.Property(e => e.HotelName).HasMaxLength(100);
            entity.Property(e => e.Nationality).HasMaxLength(60);
            entity.Property(e => e.PassportNo).HasMaxLength(9);
            entity.Property(e => e.RoomNo).HasMaxLength(20);

            entity.HasOne(d => d.User).WithOne(p => p.TouristProfile)
                .HasForeignKey<TouristProfile>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__TouristPr__UserI__59FA5E80");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__Users__1788CC4CA0F20228");

            entity.HasIndex(e => e.Identifier, "UQ__Users__821FB019D6C0C19B").IsUnique();

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.Identifier).HasMaxLength(30);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.PasswordHash).HasMaxLength(200);
            entity.Property(e => e.Role).HasMaxLength(20);

            entity.HasOne(d => d.Hub).WithMany(p => p.Users)
                .HasForeignKey(d => d.HubId)
                .HasConstraintName("FK__Users__HubId__5165187F");
        });

        modelBuilder.Entity<Voucher>(entity =>
        {
            entity.HasKey(e => e.Code).HasName("PK__Vouchers__A25C5AA6C2F938B0");

            entity.Property(e => e.Code).HasMaxLength(30);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.MaxDiscount).HasColumnType("decimal(12, 2)");
            entity.Property(e => e.MinSubtotal).HasColumnType("decimal(12, 2)");
            entity.Property(e => e.Type).HasMaxLength(10);
            entity.Property(e => e.Value).HasColumnType("decimal(12, 2)");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
