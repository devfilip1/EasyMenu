using EasyMenu.Domain.Establishments;
using EasyMenu.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EasyMenu.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();
        builder.Property(u => u.Name).HasMaxLength(User.NameMaxLength);
        builder.Property(u => u.Email).HasMaxLength(User.EmailMaxLength).IsRequired();
        builder.Property(u => u.NormalizedEmail).HasMaxLength(User.EmailMaxLength).IsRequired();
        builder.Property(u => u.PasswordHash).IsRequired();
        builder.Property(u => u.Role).HasMaxLength(20).IsRequired();
        builder.Property(u => u.SecurityStamp).HasMaxLength(User.SecurityStampMaxLength).IsRequired();

        builder.HasIndex(u => u.NormalizedEmail).IsUnique();
        builder.HasIndex(u => u.EstablishmentId);
        builder.HasOne<Establishment>().WithMany().HasForeignKey(u => u.EstablishmentId).OnDelete(DeleteBehavior.Restrict);
    }
}
