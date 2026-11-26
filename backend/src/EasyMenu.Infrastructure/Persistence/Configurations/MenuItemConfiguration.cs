using EasyMenu.Domain.Establishments;
using EasyMenu.Domain.Menu;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EasyMenu.Infrastructure.Persistence.Configurations;

internal sealed class MenuItemConfiguration : IEntityTypeConfiguration<MenuItem>
{
    public void Configure(EntityTypeBuilder<MenuItem> builder)
    {
        builder.ToTable("menu_items");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.Property(i => i.Name).HasMaxLength(MenuItem.NameMaxLength).IsRequired();
        builder.Property(i => i.Description).HasMaxLength(MenuItem.DescriptionMaxLength);
        builder.Property(i => i.PhotoPath).HasMaxLength(MenuItem.PhotoPathMaxLength);
        builder.Property(i => i.Station).HasMaxLength(20).IsRequired();
        builder.Ignore(i => i.EffectivePriceCents);

        builder.HasIndex(i => new { i.EstablishmentId, i.CategoryId, i.Position });
        builder.HasOne<Establishment>().WithMany().HasForeignKey(i => i.EstablishmentId).OnDelete(DeleteBehavior.Restrict);
        // Composite FK: an item can only point to a category of its own establishment.
        builder.HasOne<Category>().WithMany()
            .HasForeignKey(i => new { i.EstablishmentId, i.CategoryId })
            .HasPrincipalKey(c => new { c.EstablishmentId, c.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
