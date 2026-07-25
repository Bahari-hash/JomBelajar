using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TinyLang.Entities;

namespace TinyLang.Database.Configurations;

/// <summary>
/// 配置文章与媒体资源关联的复合键和删除行为。
/// </summary>
public sealed class ArticleMediaResourceConfiguration : IEntityTypeConfiguration<ArticleMediaResource>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ArticleMediaResource> builder)
    {
        builder.ToTable("article_media_resources");

        builder.HasKey(x => new { x.ArticleId, x.MediaResourceId });

        builder.HasOne(x => x.Article)
            .WithMany(x => x.MediaResources)
            .HasForeignKey(x => x.ArticleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.MediaResource)
            .WithMany(x => x.ArticleMediaResources)
            .HasForeignKey(x => x.MediaResourceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
