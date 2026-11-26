using EasyMenu.Domain.Establishments;
using EasyMenu.Domain.Menu;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EasyMenu.Infrastructure.Persistence.Configurations;

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Name).HasMaxLength(Category.NameMaxLength).IsRequired();

        // Principal of the composite tenant FK from menu_items (constitution III, defence in depth).
        builder.HasAlternateKey(c => new { c.EstablishmentId, c.Id });
        builder.HasIndex(c => new { c.EstablishmentId, c.Position });
        builder.HasOne<Establishment>().WithMany().HasForeignKey(c => c.EstablishmentId).OnDelete(DeleteBehavior.Restrict);
    }
}
