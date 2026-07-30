using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TinyLang.Entities;

namespace TinyLang.Database.Configurations;

/// <summary>
/// 配置文章与分类关联的复合键、索引和级联关系。
/// </summary>
public sealed class ArticleCategoryAssignmentConfiguration
    : IEntityTypeConfiguration<ArticleCategoryAssignment>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ArticleCategoryAssignment> builder)
    {
        builder.ToTable("article_category_assignments");

        builder.HasKey(x => new { x.ArticleId, x.ArticleCategoryId });

        builder.HasIndex(x => new { x.ArticleCategoryId, x.ArticleId });

        builder.HasOne(x => x.Article)
            .WithMany(x => x.CategoryAssignments)
            .HasForeignKey(x => x.ArticleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ArticleCategory)
            .WithMany(x => x.ArticleAssignments)
            .HasForeignKey(x => x.ArticleCategoryId)
            .HasConstraintName("FK_article_category_assignments_article_categories")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
