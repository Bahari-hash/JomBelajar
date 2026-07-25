using TinyLang.Entities.Common;
using TinyLang.Entities.Enums;

namespace TinyLang.Entities;

/// <summary>
/// 持久化一个由服务端控制的 S3 Multipart Upload 会话及其恢复信息。
/// </summary>
public sealed class MultipartUploadSession : BaseAuditableEntity
{
    /// <summary>
    /// 创建具有独立应用标识的 Multipart Upload 会话。
    /// </summary>
    public MultipartUploadSession()
    {
        Id = Guid.NewGuid();
    }

    public Guid MediaResourceId { get; set; }
    public MediaResource MediaResource { get; set; } = null!;
    public Guid UploaderId { get; set; }
    public User Uploader { get; set; } = null!;
    public required string ProviderUploadId { get; set; }
    public MultipartUploadStatus Status { get; set; } = MultipartUploadStatus.Initiated;
    public long PartSize { get; set; }
    public int PartCount { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public int AttemptCount { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public Guid? LeaseOwner { get; set; }
    public DateTimeOffset? LeaseExpiresAt { get; set; }
    public bool StagingCleanupRequired { get; set; }
    public string? LastFailureCode { get; set; }
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
}
