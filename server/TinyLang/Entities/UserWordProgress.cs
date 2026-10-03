using TinyLang.Entities.Common;
using TinyLang.Entities.Enums;

namespace TinyLang.Entities;

/// <summary>
/// 保存一个用户对一个全局词条已经完成的累计背诵结果。
/// </summary>
public sealed class UserWordProgress : BaseAuditableEntity
{
    /// <summary>
    /// 创建具有独立标识的用户词条进度。
    /// </summary>
    public UserWordProgress()
    {
        Id = Guid.NewGuid();
    }

    public Guid UserId { get; set; }
    public User? User { get; set; }
    public Guid WordId { get; set; }
    public Word? Word { get; set; }
    public DateTimeOffset FirstStudiedAt { get; set; }
    public DateTimeOffset LastStudiedAt { get; set; }
    public DateTimeOffset? LastReviewedAt { get; set; }
    // Null state identifies legacy progress: preserve its due date until first FSRS rating.
    public string? FsrsState { get; set; }
    public double? Stability { get; set; }
    public double? Difficulty { get; set; }
    public int? LearningStep { get; set; }
    public DateTimeOffset? LastRatedAt { get; set; }
    public string? SchedulerVersion { get; set; }
    public bool ImportedFromLegacy { get; set; }
    public int ReviewStage { get; set; }
    public DateTimeOffset? NextReviewAt { get; set; }
    public int ReviewCount { get; set; }
    public int SuccessfulReviewCount { get; set; }
    public int FailedReviewCount { get; set; }
    public bool IsReviewExcluded { get; set; }
    public DateTimeOffset? ReviewExcludedAt { get; set; }
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
}
