using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TinyLang.Entities;

namespace TinyLang.Database.Configurations;

/// <summary>
/// 配置 HLS rendition 字段、版本唯一性和视频关系。
/// </summary>
public sealed class VideoRenditionConfiguration : IEntityTypeConfiguration<VideoRendition>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<VideoRendition> builder)
    {
        builder.ToTable("video_renditions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PlaylistObjectName).HasMaxLength(512).IsRequired();
        builder.Property(x => x.Codecs).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => new { x.VideoId, x.OutputVersion, x.TargetHeight })
            .IsUnique();
        builder.HasOne(x => x.Video)
            .WithMany(x => x.Renditions)
            .HasForeignKey(x => x.VideoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
