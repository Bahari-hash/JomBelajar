using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TinyLang.Entities;

namespace TinyLang.Database.Configurations;

/// <summary>
/// 配置视频处理任务的输出版本、租约查询和并发约束。
/// </summary>
public sealed class VideoProcessingJobConfiguration
    : IEntityTypeConfiguration<VideoProcessingJob>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<VideoProcessingJob> builder)
    {
        builder.ToTable("video_processing_jobs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status)
            .HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.FailureCode).HasMaxLength(100);
        builder.Property(x => x.ConcurrencyStamp).IsConcurrencyToken();

        builder.HasIndex(x => new { x.VideoId, x.OutputVersion }).IsUnique();
        builder.HasIndex(x => x.VideoId)
            .IsUnique()
            .HasFilter("\"Status\" IN ('Queued', 'Processing')");
        builder.HasIndex(x => new
        {
            x.Status,
            x.NextAttemptAt,
            x.LeaseExpiresAt,
            x.LastDispatchedAt,
            x.CreatedAt,
            x.Id
        });

        builder.HasOne(x => x.Video)
            .WithMany(x => x.ProcessingJobs)
            .HasForeignKey(x => x.VideoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
