using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TinyLang.Entities;

namespace TinyLang.Database.Configurations;

/// <summary>
/// 配置用户账户、资料字段和唯一身份索引。
/// </summary>
public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users", table => table.HasCheckConstraint(
            "CK_users_daily_word_study_count",
            "\"DailyWordStudyCount\" BETWEEN 1 AND 100"));

        builder.HasKey(x => x.Id);

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
        builder.Property(x => x.DailyWordStudyCount)
            .HasDefaultValue(20);
        builder.Property(x => x.BannedReason)
            .HasMaxLength(500);

        builder.HasIndex(x => x.Email)
            .IsUnique();

        builder.HasIndex(x => new { x.IsDeleted, x.CreatedAt, x.Id });
        builder.HasIndex(x => new { x.IsDeleted, x.Role, x.CreatedAt, x.Id });
        builder.HasIndex(x => new { x.IsDeleted, x.IsBanned, x.CreatedAt, x.Id });
    }
}
