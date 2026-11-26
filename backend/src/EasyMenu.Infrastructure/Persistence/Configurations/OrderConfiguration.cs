using EasyMenu.Application.Orders;
using EasyMenu.Domain.Establishments;
using EasyMenu.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EasyMenu.Infrastructure.Persistence.Configurations;

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    /// <summary>Shadow concurrency token mapped by Npgsql to the PostgreSQL <c>xmin</c> system column.</summary>
    public const string VersionProperty = "Version";

    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();
        builder.Property(o => o.PublicToken).HasMaxLength(PublicToken.Length).IsFixedLength().IsRequired();
        builder.Property(o => o.PickupCode).HasMaxLength(PickupCode.Length).IsFixedLength().IsRequired();
        builder.Property(o => o.CustomerName).HasMaxLength(Order.CustomerNameMaxLength);
        builder.Property(o => o.Status).HasMaxLength(20).IsRequired();
        builder.Property(o => o.PaymentTransactionId).HasMaxLength(100);
        builder.Property<uint>(VersionProperty).IsRowVersion();

        builder.HasIndex(o => o.PublicToken).IsUnique().HasDatabaseName(CheckoutService.PublicTokenIndex);
        // A pickup code can be reused once its order was picked up (FR-021).
        builder.HasIndex(o => new { o.EstablishmentId, o.PickupCode })
            .IsUnique()
            .HasFilter("status <> 'picked_up'")
            .HasDatabaseName(CheckoutService.PickupCodeIndex);
        builder.HasIndex(o => new { o.EstablishmentId, o.Status, o.CreatedAt }).IsDescending(false, false, true);
        builder.HasOne<Establishment>().WithMany().HasForeignKey(o => o.EstablishmentId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(o => o.Items).WithOne().HasForeignKey("OrderId").IsRequired().OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(o => o.Parts).WithOne().HasForeignKey("OrderId").IsRequired().OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(o => o.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(o => o.Parts).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("order_items");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();
        // Logical reference only (no FK): the menu item may be deleted, the order keeps its snapshot.
        builder.Property(i => i.MenuItemId).IsRequired();
        builder.Property(i => i.NameSnapshot).HasMaxLength(100).IsRequired();
        builder.Property(i => i.Station).HasMaxLength(20).IsRequired();
    }
}

internal sealed class OrderPartConfiguration : IEntityTypeConfiguration<OrderPart>
{
    public void Configure(EntityTypeBuilder<OrderPart> builder)
    {
        builder.ToTable("order_parts");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Station).HasMaxLength(20).IsRequired();
        builder.Property(p => p.Status).HasMaxLength(20).IsRequired();
        builder.HasIndex("OrderId", nameof(OrderPart.Station)).IsUnique();
    }
}
