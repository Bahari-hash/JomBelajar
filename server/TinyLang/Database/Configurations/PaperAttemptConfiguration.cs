using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TinyLang.Entities;

namespace TinyLang.Database.Configurations;

/// <summary>
/// 配置测验编号、活动唯一性、评分快照、状态一致性和历史索引。
/// </summary>
public sealed class PaperAttemptConfiguration
    : IEntityTypeConfiguration<PaperAttempt>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PaperAttempt> builder)
    {
        builder.ToTable("paper_attempts", table =>
        {
            table.HasCheckConstraint(
                "CK_paper_attempts_number",
                "\"AttemptNumber\" >= 1");
            table.HasCheckConstraint(
                "CK_paper_attempts_snapshot_scores",
                "\"PaperTotalScore\" BETWEEN 0 AND 20000 AND " +
                "\"PaperPassingScore\" BETWEEN 0 AND \"PaperTotalScore\"");
            table.HasCheckConstraint(
                "CK_paper_attempts_submission_state",
                "(\"Status\" = 'InProgress' AND \"Score\" IS NULL AND " +
                "\"IsPassed\" IS NULL AND \"SubmittedAt\" IS NULL) OR " +
                "(\"Status\" = 'Submitted' AND \"Score\" IS NOT NULL AND " +
                "\"Score\" BETWEEN 0 AND \"PaperTotalScore\" AND " +
                "\"IsPassed\" IS NOT NULL AND \"SubmittedAt\" IS NOT NULL)");
        });
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Status)
            .HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(value => value.ConcurrencyStamp).IsConcurrencyToken();

        builder.HasIndex(value => new
        {
            value.UserId,
            value.PaperId,
            value.AttemptNumber
        }).IsUnique();
        builder.HasIndex(value => new { value.UserId, value.PaperId })
            .IsUnique()
            .HasFilter("\"Status\" = 'InProgress'");
        builder.HasIndex(value => new
        {
            value.UserId,
            value.StartedAt,
            value.Id
        });
        builder.HasIndex(value => new
        {
            value.UserId,
            value.PaperId,
            value.StartedAt,
            value.Id
        });

        builder.HasOne(value => value.Paper)
            .WithMany(value => value.Attempts)
            .HasForeignKey(value => value.PaperId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(value => value.User)
            .WithMany(value => value.PaperAttempts)
            .HasForeignKey(value => value.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
