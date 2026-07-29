using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TinyLang.Entities;

namespace TinyLang.Database.Configurations;

/// <summary>
/// 配置填空题标准答案文本、比较键、唯一性和父题目关系。
/// </summary>
public sealed class FillBlankAcceptedAnswerConfiguration
    : IEntityTypeConfiguration<FillBlankAcceptedAnswer>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<FillBlankAcceptedAnswer> builder)
    {
        builder.ToTable("fill_blank_accepted_answers", table =>
            table.HasCheckConstraint(
                "CK_fill_blank_accepted_answers_sort_order",
                "\"SortOrder\" BETWEEN 0 AND 10000"));
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Text).HasMaxLength(1000).IsRequired();
        builder.Property(value => value.NormalizedText)
            .HasMaxLength(1000).IsRequired();
        builder.HasIndex(value => new { value.QuestionId, value.SortOrder }).IsUnique();
        builder.HasIndex(value => new { value.QuestionId, value.NormalizedText })
            .IsUnique();
        builder.HasOne(value => value.Question)
            .WithMany(value => value.AcceptedAnswers)
            .HasForeignKey(value => value.QuestionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
