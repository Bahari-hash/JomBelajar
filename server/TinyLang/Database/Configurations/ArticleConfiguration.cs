using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TinyLang.Dtos;
using TinyLang.Entities;

namespace TinyLang.Database.Configurations;

/// <summary>
/// 配置文章字段、查询索引和用户/封面关系。
/// </summary>
public sealed class ArticleConfiguration : IEntityTypeConfiguration<Article>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Article> builder)
    {
        builder.ToTable("articles");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title)
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(x => x.Summary)
            .HasMaxLength(500);
        builder.Property(x => x.ContentMarkdown)
            .HasMaxLength(ArticleConstraints.MaxContentLength)
            .IsRequired();
        builder.Property(x => x.ContentHtml)
            .HasMaxLength(ArticleConstraints.MaxContentLength)
            .IsRequired();
        builder.Property(x => x.ConcurrencyStamp)
            .IsConcurrencyToken();
        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.HasIndex(x => new { x.Status, x.PublishedAt });
        builder.HasIndex(x => new { x.AuthorId, x.CreatedAt });
        builder.HasIndex(x => new { x.LastEditorId, x.UpdatedAt });

        builder.HasOne(x => x.Author)
            .WithMany(x => x.AuthoredArticles)
            .HasForeignKey(x => x.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.LastEditor)
            .WithMany(x => x.EditedArticles)
            .HasForeignKey(x => x.LastEditorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.PublishedBy)
            .WithMany(x => x.PublishedArticles)
            .HasForeignKey(x => x.PublishedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CoverMediaResource)
            .WithMany(x => x.CoveredArticles)
            .HasForeignKey(x => x.CoverMediaResourceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
