using TinyLang.Entities.Common;

namespace TinyLang.Entities;

/// <summary>
/// 记录用户完成当天新词学习后的 UTC 打卡。
/// </summary>
public sealed class WordStudyCheckIn : BaseEntity
{
    /// <summary>
    /// 创建具有独立标识的打卡记录。
    /// </summary>
    public WordStudyCheckIn()
    {
        Id = Guid.NewGuid();
    }

    public Guid UserId { get; set; }
    public User? User { get; set; }
    public DateTimeOffset StudyDateUtc { get; set; }
    public DateTimeOffset CheckedInAtUtc { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
