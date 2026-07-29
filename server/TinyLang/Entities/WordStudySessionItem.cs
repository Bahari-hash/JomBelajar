using TinyLang.Entities.Common;
using TinyLang.Entities.Enums;

namespace TinyLang.Entities;

/// <summary>
/// 固定一次背诵会话中的词条标识、位置和最终处理状态。
/// </summary>
public sealed class WordStudySessionItem : BaseEntity
{
    /// <summary>
    /// 创建具有独立标识和并发标识的待处理会话项。
    /// </summary>
    public WordStudySessionItem()
    {
        Id = Guid.NewGuid();
    }

    public Guid SessionId { get; set; }
    public WordStudySession? Session { get; set; }
    public Guid WordId { get; set; }
    public Word? Word { get; set; }
    public int Position { get; set; }
    public WordStudySessionItemStatus Status { get; set; } =
        WordStudySessionItemStatus.Pending;
    public DateTimeOffset? AnsweredAt { get; set; }
    public WordStudySkipReason? SkipReason { get; set; }
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
}
