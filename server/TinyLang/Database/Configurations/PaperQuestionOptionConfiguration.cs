using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TinyLang.Entities;

namespace TinyLang.Database.Configurations;

/// <summary>
/// 配置单选题选项文本、顺序唯一性和父题目关系。
/// </summary>
public sealed class PaperQuestionOptionConfiguration
    : IEntityTypeConfiguration<PaperQuestionOption>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PaperQuestionOption> builder)
    {
        builder.ToTable("paper_question_options", table =>
            table.HasCheckConstraint(
                "CK_paper_question_options_sort_order",
                "\"SortOrder\" BETWEEN 0 AND 10000"));
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Text).HasMaxLength(2000).IsRequired();
        builder.HasIndex(value => new { value.QuestionId, value.SortOrder }).IsUnique();
        builder.HasOne(value => value.Question)
            .WithMany(value => value.Options)
            .HasForeignKey(value => value.QuestionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
