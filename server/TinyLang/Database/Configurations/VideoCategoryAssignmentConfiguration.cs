using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TinyLang.Entities;

namespace TinyLang.Database.Configurations;

/// <summary>
/// 配置视频分类关联的复合键、反向索引和级联删除关系。
/// </summary>
public sealed class VideoCategoryAssignmentConfiguration
    : IEntityTypeConfiguration<VideoCategoryAssignment>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<VideoCategoryAssignment> builder)
    {
        builder.ToTable("video_category_assignments");
        builder.HasKey(value => new { value.VideoId, value.VideoCategoryId });
        builder.HasIndex(value => new { value.VideoCategoryId, value.VideoId });
        builder.HasOne(value => value.Video)
            .WithMany(value => value.CategoryAssignments)
            .HasForeignKey(value => value.VideoId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(value => value.VideoCategory)
            .WithMany(value => value.VideoAssignments)
            .HasForeignKey(value => value.VideoCategoryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
