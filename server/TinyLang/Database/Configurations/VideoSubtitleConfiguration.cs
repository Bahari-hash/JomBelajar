using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TinyLang.Entities;

namespace TinyLang.Database.Configurations;

/// <summary>
/// 配置视频字幕的资源占用、语言和默认字幕唯一约束。
/// </summary>
public sealed class VideoSubtitleConfiguration : IEntityTypeConfiguration<VideoSubtitle>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<VideoSubtitle> builder)
    {
        builder.ToTable("video_subtitles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.LanguageTag).HasMaxLength(35).IsRequired();
        builder.Property(x => x.DisplayName).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => x.MediaResourceId).IsUnique();
        builder.HasIndex(x => new { x.VideoId, x.LanguageTag }).IsUnique();
        builder.HasIndex(x => x.VideoId)
            .IsUnique()
            .HasFilter("\"IsDefault\" = TRUE");
        builder.HasOne(x => x.Video)
            .WithMany(x => x.Subtitles)
            .HasForeignKey(x => x.VideoId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.MediaResource)
            .WithOne(x => x.VideoSubtitle)
            .HasForeignKey<VideoSubtitle>(x => x.MediaResourceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
