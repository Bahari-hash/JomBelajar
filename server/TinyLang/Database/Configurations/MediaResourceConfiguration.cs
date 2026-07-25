using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TinyLang.Entities;

namespace TinyLang.Database.Configurations;

/// <summary>
/// 配置媒体资源字段约束、索引和上传者关系。
/// </summary>
public sealed class MediaResourceConfiguration : IEntityTypeConfiguration<MediaResource>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<MediaResource> builder)
    {
        builder.ToTable("media_resources");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ObjectName)
            .HasMaxLength(512)
            .IsRequired();
        builder.Property(x => x.StagingObjectName)
            .HasMaxLength(512);
        builder.Property(x => x.OriginalName)
            .HasMaxLength(255)
            .IsRequired();
        builder.Property(x => x.Module)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(x => x.Extension)
            .HasMaxLength(16)
            .IsRequired();
        builder.Property(x => x.ContentType)
            .HasMaxLength(100)
            .IsRequired();
        builder.Property(x => x.Url)
            .HasMaxLength(2048);
        builder.Property(x => x.ConcurrencyStamp)
            .IsConcurrencyToken();

        builder.HasIndex(x => x.ObjectName)
            .IsUnique();
        builder.HasIndex(x => x.StagingObjectName)
            .IsUnique();
        builder.HasIndex(x => new { x.UploaderId, x.Status });
        builder.HasIndex(x => new { x.Status, x.UploadExpiresAt, x.Id });

        builder.HasOne(x => x.Uploader)
            .WithMany(x => x.MediaResources)
            .HasForeignKey(x => x.UploaderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
