using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TinyLang.Entities;

namespace TinyLang.Database.Configurations;

/// <summary>
/// 配置背诵会话的活动唯一性、计数边界和用户生命周期关系。
/// </summary>
public sealed class WordStudySessionConfiguration
    : IEntityTypeConfiguration<WordStudySession>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<WordStudySession> builder)
    {
        builder.ToTable("word_study_sessions", table =>
            table.HasCheckConstraint(
                "CK_word_study_sessions_counts",
                "\"RequestedCount\" BETWEEN 1 AND 100 AND " +
                "\"ActualCount\" BETWEEN 1 AND \"RequestedCount\""));
        builder.HasKey(value => value.Id);
        builder.Property(value => value.SelectionMode)
            .HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(value => value.Status)
            .HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(value => value.StudyDateUtc).IsRequired();
        builder.Property(value => value.ConcurrencyStamp).IsConcurrencyToken();

        builder.HasIndex(value => value.UserId)
            .IsUnique()
            .HasFilter("\"Status\" = 'Active'");
        builder.HasIndex(value => new
        {
            value.UserId,
            value.StartedAt,
            value.Id
        });
        builder.HasIndex(value => new { value.UserId, value.StudyDateUtc })
            .IsUnique();

        builder.HasOne(value => value.User)
            .WithMany(value => value.WordStudySessions)
            .HasForeignKey(value => value.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
