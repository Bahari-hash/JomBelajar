using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TinyLang.Entities;

namespace TinyLang.Database.Configurations;

/// <summary>
/// 配置试卷字段、评分边界、查询索引、并发和审计关系。
/// </summary>
public sealed class PaperConfiguration : IEntityTypeConfiguration<Paper>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Paper> builder)
    {
        builder.ToTable("papers", table => table.HasCheckConstraint(
            "CK_papers_scores",
            "\"TotalScore\" BETWEEN 0 AND 20000 AND " +
            "\"PassingScorePercentage\" BETWEEN 1 AND 100 AND " +
            "\"PassingScore\" BETWEEN 0 AND \"TotalScore\""));
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Title).HasMaxLength(200).IsRequired();
        builder.Property(value => value.Description).HasMaxLength(2000);
        builder.Property(value => value.Instructions).HasMaxLength(5000);
        builder.Property(value => value.Tags)
            .HasColumnType("text[]")
            .HasDefaultValueSql("'{}'::text[]")
            .IsRequired();
        builder.HasIndex(value => value.Tags).HasMethod("gin");
        builder.Property(value => value.PassingScorePercentage)
            .HasDefaultValue(60);
        builder.Property(value => value.Status)
            .HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(value => value.ConcurrencyStamp).IsConcurrencyToken();

        builder.HasIndex(value => new
        {
            value.Status,
            value.PublishedAt,
            value.Id
        });
        builder.HasIndex(value => new
        {
            value.Status,
            value.UpdatedAt,
            value.Id
        });

        builder.HasOne(value => value.CreatedBy)
            .WithMany(value => value.CreatedPapers)
            .HasForeignKey(value => value.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(value => value.LastEditor)
            .WithMany(value => value.EditedPapers)
            .HasForeignKey(value => value.LastEditorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
