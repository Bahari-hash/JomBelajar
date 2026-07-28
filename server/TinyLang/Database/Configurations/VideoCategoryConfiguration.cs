using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TinyLang.Entities;

namespace TinyLang.Database.Configurations;

/// <summary>
/// 配置视频分类字段长度、启用状态和名称/slug 唯一约束。
/// </summary>
public sealed class VideoCategoryConfiguration : IEntityTypeConfiguration<VideoCategory>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<VideoCategory> builder)
    {
        builder.ToTable("video_categories");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Name)
            .HasMaxLength(100)
            .IsRequired();
        builder.Property(value => value.Slug)
            .HasMaxLength(120)
            .IsRequired();
        builder.Property(value => value.Description)
            .HasMaxLength(500);
        builder.Property(value => value.IsActive)
            .HasDefaultValue(true)
            .IsRequired();
        builder.HasIndex(value => value.Name).IsUnique();
        builder.HasIndex(value => value.Slug).IsUnique();
    }
}
