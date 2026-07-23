using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TinyLang.Entities;

namespace TinyLang.Database.Configurations;

public sealed class ArticleMediaResourceConfiguration : IEntityTypeConfiguration<ArticleMediaResource>
{
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
