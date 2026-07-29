using TinyLang.Entities.Common;
using TinyLang.Entities.Enums;

namespace TinyLang.Entities;

/// <summary>
/// 表示一个用户针对固定试卷内容创建的可恢复测验记录。
/// </summary>
public sealed class PaperAttempt : BaseEntity
{
    /// <summary>
    /// 创建具有独立标识和并发标识的测验记录。
    /// </summary>
    public PaperAttempt()
    {
        Id = Guid.NewGuid();
    }

    public Guid PaperId { get; set; }
    public Paper? Paper { get; set; }
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public int AttemptNumber { get; set; }
    public PaperAttemptStatus Status { get; set; } = PaperAttemptStatus.InProgress;
    public int PaperTotalScore { get; set; }
    public int PaperPassingScore { get; set; }
    public int? Score { get; set; }
    public bool? IsPassed { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
    public ICollection<PaperAttemptAnswer> Answers { get; set; } = [];
}
