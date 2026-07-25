using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TinyLang.Entities;

namespace TinyLang.Database.Configurations;

/// <summary>
/// 配置文章分类字段约束和唯一索引。
/// </summary>
public sealed class ArticleCategoryConfiguration : IEntityTypeConfiguration<ArticleCategory>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ArticleCategory> builder)
    {
        builder.ToTable("article_categories");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .HasMaxLength(100)
            .IsRequired();
        builder.Property(x => x.Slug)
            .HasMaxLength(120)
            .IsRequired();
        builder.Property(x => x.Description)
            .HasMaxLength(500);
        builder.Property(x => x.IsActive)
            .HasDefaultValue(true)
            .IsRequired();

        builder.HasIndex(x => x.Name)
            .IsUnique();
        builder.HasIndex(x => x.Slug)
            .IsUnique();
    }
}
