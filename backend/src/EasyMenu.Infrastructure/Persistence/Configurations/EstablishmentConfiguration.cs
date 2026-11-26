using EasyMenu.Domain.Establishments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EasyMenu.Infrastructure.Persistence.Configurations;

internal sealed class EstablishmentConfiguration : IEntityTypeConfiguration<Establishment>
{
    public void Configure(EntityTypeBuilder<Establishment> builder)
    {
        builder.ToTable("establishments");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Name).HasMaxLength(Establishment.NameMaxLength).IsRequired();
        builder.Property(e => e.Slug).HasMaxLength(Slug.MaxLength).IsRequired();
        builder.HasIndex(e => e.Slug).IsUnique();
    }
}
