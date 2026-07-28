using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TinyLang.Entities;

namespace TinyLang.Database.Configurations;

/// <summary>
/// 配置音频处理任务的输出版本、租约查询和 active-job 并发约束。
/// </summary>
public sealed class AudioProcessingJobConfiguration
    : IEntityTypeConfiguration<AudioProcessingJob>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AudioProcessingJob> builder)
    {
        builder.ToTable("audio_processing_jobs");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Status)
            .HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(value => value.FailureCode).HasMaxLength(100);
        builder.Property(value => value.ConcurrencyStamp).IsConcurrencyToken();

        builder.HasIndex(value => new { value.AudioClipId, value.OutputVersion }).IsUnique();
        builder.HasIndex(value => value.AudioClipId)
            .IsUnique()
            .HasFilter("\"Status\" IN ('Queued', 'Processing')");
        builder.HasIndex(value => new
        {
            value.Status,
            value.NextAttemptAt,
            value.LeaseExpiresAt,
            value.LastDispatchedAt,
            value.CreatedAt,
            value.Id
        });

        builder.HasOne(value => value.AudioClip)
            .WithMany(value => value.ProcessingJobs)
            .HasForeignKey(value => value.AudioClipId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
