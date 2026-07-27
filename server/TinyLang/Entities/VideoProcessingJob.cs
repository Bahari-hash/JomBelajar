using TinyLang.Entities.Common;
using TinyLang.Entities.Enums;

namespace TinyLang.Entities;

/// <summary>
/// 持久化一次不可变输出版本的视频处理任务及其租约和重试信息。
/// </summary>
public sealed class VideoProcessingJob : BaseAuditableEntity
{
    /// <summary>
    /// 创建具有独立任务标识的视频处理任务。
    /// </summary>
    public VideoProcessingJob()
    {
        Id = Guid.NewGuid();
    }

    public Guid VideoId { get; set; }
    public Video Video { get; set; } = null!;
    public Guid OutputVersion { get; set; }
    public VideoProcessingJobStatus Status { get; set; } = VideoProcessingJobStatus.Queued;
    public int AttemptCount { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public DateTimeOffset? LastDispatchedAt { get; set; }
    public Guid? LeaseOwner { get; set; }
    public DateTimeOffset? LeaseExpiresAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? FailureCode { get; set; }
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
}
