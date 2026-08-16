using TinyLang.Entities.Common;
using TinyLang.Entities.Enums;

namespace TinyLang.Entities;

/// <summary>
/// 持久化一次不可变输出版本的音频处理任务及其租约和重试信息。
/// </summary>
public sealed class AudioProcessingJob : BaseAuditableEntity
{
    /// <summary>
    /// 创建具有独立任务标识的音频处理任务。
    /// </summary>
    public AudioProcessingJob()
    {
        Id = Guid.NewGuid();
    }

    public Guid? AudioResourceId { get; set; }
    public AudioResource? AudioResource { get; set; }

    // 保留到音频服务切换完成，避免旧处理链路在重构期间失去关系映射。
    public Guid? AudioClipId { get; set; }

    public AudioClip? AudioClip { get; set; }
    public Guid OutputVersion { get; set; }
    public AudioProcessingJobStatus Status { get; set; } = AudioProcessingJobStatus.Queued;
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
