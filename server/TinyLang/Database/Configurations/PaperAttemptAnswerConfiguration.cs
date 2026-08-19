using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TinyLang.Entities;

namespace TinyLang.Database.Configurations;

/// <summary>
/// 配置测验答案形状、判分边界、唯一性、并发和历史保护关系。
/// </summary>
public sealed class PaperAttemptAnswerConfiguration
    : IEntityTypeConfiguration<PaperAttemptAnswer>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PaperAttemptAnswer> builder)
    {
        builder.ToTable("paper_attempt_answers", table =>
        {
            table.HasCheckConstraint(
                "CK_paper_attempt_answers_shape",
                "(\"IsAnswered\" = FALSE AND \"SelectedOptionId\" IS NULL AND " +
                "\"BooleanAnswer\" IS NULL AND \"TextAnswer\" IS NULL AND " +
                "\"NormalizedTextAnswer\" IS NULL AND \"TextAnswers\" IS NULL) OR " +
                "(\"IsAnswered\" = TRUE AND ((\"SelectedOptionId\" IS NOT NULL AND " +
                "\"BooleanAnswer\" IS NULL AND \"TextAnswer\" IS NULL AND " +
                "\"NormalizedTextAnswer\" IS NULL AND \"TextAnswers\" IS NULL) OR " +
                "(\"SelectedOptionId\" IS NULL AND " +
                "\"BooleanAnswer\" IS NOT NULL AND \"TextAnswer\" IS NULL AND " +
                "\"NormalizedTextAnswer\" IS NULL AND \"TextAnswers\" IS NULL) OR " +
                "(\"SelectedOptionId\" IS NULL AND " +
                "\"BooleanAnswer\" IS NULL AND \"TextAnswer\" IS NOT NULL AND " +
                "\"NormalizedTextAnswer\" IS NOT NULL AND \"TextAnswers\" IS NULL) OR " +
                "(\"SelectedOptionId\" IS NULL AND \"BooleanAnswer\" IS NULL AND " +
                "\"TextAnswer\" IS NULL AND \"NormalizedTextAnswer\" IS NULL AND " +
                "\"TextAnswers\" IS NOT NULL)))");
            table.HasCheckConstraint(
                "CK_paper_attempt_answers_awarded_points",
                "(\"IsCorrect\" IS NULL AND \"AwardedPoints\" IS NULL) OR " +
                "(\"IsCorrect\" IS NOT NULL AND \"AwardedPoints\" BETWEEN 0 AND 100)");
        });
        builder.HasKey(value => value.Id);
        builder.Property(value => value.TextAnswer).HasMaxLength(1000);
        builder.Property(value => value.NormalizedTextAnswer).HasMaxLength(1000);
        builder.Property(value => value.TextAnswers)
            .HasColumnType("text[]");
        builder.Property(value => value.ConcurrencyStamp).IsConcurrencyToken();

        builder.HasIndex(value => new { value.AttemptId, value.QuestionId }).IsUnique();
        builder.HasOne(value => value.Attempt)
            .WithMany(value => value.Answers)
            .HasForeignKey(value => value.AttemptId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(value => value.Question)
            .WithMany(value => value.AttemptAnswers)
            .HasForeignKey(value => value.QuestionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(value => value.SelectedOption)
            .WithMany(value => value.SelectedByAnswers)
            .HasForeignKey(value => value.SelectedOptionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
