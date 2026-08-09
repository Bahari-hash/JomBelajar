using TinyLang.Entities.Common;
using TinyLang.Entities.Enums;

namespace TinyLang.Entities;

/// <summary>
/// 表示一个用户创建并固定选词结果的基础单词背诵会话。
/// </summary>
public sealed class WordStudySession : BaseAuditableEntity
{
    /// <summary>
    /// 创建具有独立标识和并发标识的活动会话。
    /// </summary>
    public WordStudySession()
    {
        Id = Guid.NewGuid();
    }

    public Guid UserId { get; set; }
    public User? User { get; set; }
    public int RequestedCount { get; set; }
    public int ActualCount { get; set; }
    public DateTimeOffset StudyDateUtc { get; set; }
    public bool IncludePreviouslyStudied { get; set; }
    public WordStudySelectionMode SelectionMode { get; set; }
    public string? LanguageTag { get; set; }
    public WordStudySessionStatus Status { get; set; } = WordStudySessionStatus.Active;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? AbandonedAt { get; set; }
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
    public ICollection<WordStudySessionItem> Items { get; set; } = [];
}
