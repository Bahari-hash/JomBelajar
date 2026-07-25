using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TinyLang.Entities;

namespace TinyLang.Database.Configurations;

/// <summary>
/// 配置 Multipart Upload 会话约束、并发字段、索引和实体关系。
/// </summary>
public sealed class MultipartUploadSessionConfiguration
    : IEntityTypeConfiguration<MultipartUploadSession>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<MultipartUploadSession> builder)
    {
        builder.ToTable("multipart_upload_sessions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ProviderUploadId)
            .HasMaxLength(1024)
            .IsRequired();
        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(x => x.LastFailureCode)
            .HasMaxLength(100);
        builder.Property(x => x.ConcurrencyStamp)
            .IsConcurrencyToken();

        builder.HasIndex(x => x.MediaResourceId)
            .IsUnique();
        builder.HasIndex(x => new { x.UploaderId, x.Status });
        builder.HasIndex(x => new
        {
            x.Status,
            x.NextAttemptAt,
            x.LeaseExpiresAt,
            x.Id
        });

        builder.HasOne(x => x.MediaResource)
            .WithOne(x => x.MultipartUploadSession)
            .HasForeignKey<MultipartUploadSession>(x => x.MediaResourceId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Uploader)
            .WithMany()
            .HasForeignKey(x => x.UploaderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
