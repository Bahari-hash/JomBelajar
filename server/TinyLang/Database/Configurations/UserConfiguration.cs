using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TinyLang.Entities;

namespace TinyLang.Database.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Username)
            .HasMaxLength(100)
            .IsRequired();
        builder.Property(x => x.Email)
            .HasMaxLength(100)
            .IsRequired();
        builder.Property(x => x.PasswordHash)
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(x => x.Role)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Nickname)
            .HasMaxLength(60);
        builder.Property(x => x.AvatarUrl)
            .HasMaxLength(500);
        builder.Property(x => x.Bio)
            .HasMaxLength(500);

        builder.Property(x => x.BannedReason)
            .HasMaxLength(500);

        builder.HasIndex(x => x.Email)
            .IsUnique();
        builder.HasIndex(x => x.Username)
            .IsUnique();
    }
}
