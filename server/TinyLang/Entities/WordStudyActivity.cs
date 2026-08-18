using TinyLang.Entities.Common;
using TinyLang.Entities.Enums;

namespace TinyLang.Entities;

/// <summary>
/// 记录用户完成的一次单词学习或复习活动。
/// </summary>
public sealed class WordStudyActivity : BaseEntity
{
    /// <summary>
    /// 创建具有独立标识的学习活动记录。
    /// </summary>
    public WordStudyActivity()
    {
        Id = Guid.NewGuid();
    }

    public Guid UserId { get; set; }
    public User? User { get; set; }
    public Guid WordId { get; set; }
    public Word? Word { get; set; }
    public Guid SessionId { get; set; }
    public WordStudySession? Session { get; set; }
    public Guid SessionItemId { get; set; }
    public WordStudySessionItem? SessionItem { get; set; }
    public WordStudyActivityType ActivityType { get; set; }
    public DateTimeOffset CompletedAtUtc { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
