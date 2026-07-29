using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TinyLang.Entities;

namespace TinyLang.Database.Configurations;

/// <summary>
/// 配置试卷题目的字段边界、题型形状和同级顺序唯一性。
/// </summary>
public sealed class PaperQuestionConfiguration
    : IEntityTypeConfiguration<PaperQuestion>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PaperQuestion> builder)
    {
        builder.ToTable("paper_questions", table =>
        {
            table.HasCheckConstraint(
                "CK_paper_questions_points",
                "\"Points\" BETWEEN 1 AND 100");
            table.HasCheckConstraint(
                "CK_paper_questions_sort_order",
                "\"SortOrder\" BETWEEN 0 AND 10000");
            table.HasCheckConstraint(
                "CK_paper_questions_type_fields",
                "(\"Type\" = 'TrueFalse' OR \"CorrectBoolean\" IS NULL) AND " +
                "(\"Type\" = 'FillBlank' OR \"FillBlankCaseSensitive\" = FALSE)");
        });
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Type)
            .HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(value => value.Prompt).HasMaxLength(5000).IsRequired();
        builder.Property(value => value.Explanation).HasMaxLength(5000);
        builder.HasIndex(value => new { value.PaperId, value.SortOrder }).IsUnique();
        builder.HasOne(value => value.Paper)
            .WithMany(value => value.Questions)
            .HasForeignKey(value => value.PaperId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
