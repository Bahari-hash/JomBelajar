using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TinyLang.Entities;

namespace TinyLang.Database.Configurations;

/// <summary>
/// 配置用户词条累计进度的唯一性、计数一致性和删除边界。
/// </summary>
public sealed class UserWordProgressConfiguration
    : IEntityTypeConfiguration<UserWordProgress>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<UserWordProgress> builder)
    {
        builder.ToTable("user_word_progress", table =>
            table.HasCheckConstraint(
                "CK_user_word_progress_counts",
                "\"ReviewCount\" >= 0 AND \"RememberedCount\" >= 0 " +
                "AND \"ForgottenCount\" >= 0 AND \"ReviewCount\" = " +
                "\"RememberedCount\" + \"ForgottenCount\""));
        builder.HasKey(value => value.Id);
        builder.Property(value => value.LastResult)
            .HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.HasIndex(value => new { value.UserId, value.WordId }).IsUnique();
        builder.HasIndex(value => new
        {
            value.UserId,
            value.LastStudiedAt,
            value.WordId
        });

        builder.HasOne(value => value.User)
            .WithMany(value => value.WordProgress)
            .HasForeignKey(value => value.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(value => value.Word)
            .WithMany(value => value.UserProgress)
            .HasForeignKey(value => value.WordId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
